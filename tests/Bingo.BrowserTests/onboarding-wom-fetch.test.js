(async () => {
  const assert = require("node:assert/strict");
  const fs = require("node:fs");
  const path = require("node:path");

  const root = path.resolve(__dirname, "../..");
  const markup = fs.readFileSync(path.join(root, "src/Bingo.Web/Pages/Account/Onboarding.cshtml"), "utf8");
  const script = fs.readFileSync(path.join(root, "src/Bingo.Web/wwwroot/js/onboarding-wom-fetch.js"), "utf8");
  assert.match(markup, /type="button" data-onboarding-fetch-url='@Url\.Page\("\/Account\/Onboarding", "Fetch"\)'/);
  assert.match(markup, /class="identity-validation onboarding-validation" data-onboarding-fetch-feedback[^>]*><div asp-validation-summary="ModelOnly"><\/div><\/div>/);
  assert.doesNotMatch(script, /formAction|formaction|form\.action/);

  const { initialize } = require(path.join(root, "src/Bingo.Web/wwwroot/js/onboarding-wom-fetch.js"));
  const rootListeners = [];
  const queried = [];
  const password = { value: "Password sentinel" };
  const confirmation = { value: "Confirm sentinel" };
  const token = { name: "__RequestVerificationToken", value: "csrf-token" };
  const character = { name: "Input.OsrsCharacterName", value: "Exact Character" };
  const ehb = { value: "" };
  const feedback = { textContent: "", replaceChildren() {}, focus() {} };
  const button = {
    dataset: { onboardingFetchUrl: "/Account/Onboarding?handler=Fetch" },
    setAttribute() {},
    removeAttribute() {},
    focus() {},
    disabled: false,
    closest(selector) {
      if (selector === "[data-onboarding-fetch]") return button;
      if (selector === "[data-onboarding-form]") return form;
      return null;
    }
  };
  const form = {
    dataset: { onboardingFetchFailure: "Safe failure" },
    setAttribute() {},
    removeAttribute() {},
    querySelector(selector) {
      queried.push(selector);
      if (selector === "[data-onboarding-character]") return character;
      if (selector === "[data-onboarding-ehb]") return ehb;
      if (selector === "[data-onboarding-fetch-feedback]") return feedback;
      if (selector === '[data-valmsg-for="Input.OsrsCharacterName"]') return null;
      if (selector === 'input[name="__RequestVerificationToken"]') return token;
      return selector.includes("ConfirmPassword") ? confirmation : selector.includes("Password") ? password : null;
    }
  };
  const rootDocument = {
    addEventListener(type, handler) { if (type === "click") rootListeners.push(handler); },
    contains(node) { return node === button; }
  };
  const payloads = [];
  class TestFormData {
    constructor() { this.entries = []; payloads.push(this); }
    append(name, value) { this.entries.push([name, value]); }
  }
  let fetchCalls = 0;
  const windowObject = {
    FormData: TestFormData,
    DOMParser: class { parseFromString() { return { querySelector(selector) { return selector === "[data-onboarding-ehb]" ? { value: "42.75" } : null; } }; } },
    location: { href: "https://example.test/Account/Onboarding" },
    fetch: async (url, options) => {
      fetchCalls++;
      assert.equal(url, "https://example.test/Account/Onboarding?handler=Fetch");
      assert.equal(options.method, "POST");
      return { ok: true, redirected: false, url, text: async () => "<html></html>" };
    }
  };

  initialize(rootDocument, windowObject);
  initialize(rootDocument, windowObject);
  assert.equal(rootListeners.length, 1, "initialization must not duplicate the delegated binding");

  let nonmatchingPrevented = false;
  await rootListeners[0]({ target: { closest() { return null; }, parentElement: null }, preventDefault() { nonmatchingPrevented = true; } });
  assert.equal(fetchCalls, 0);
  assert.equal(nonmatchingPrevented, false);

  let matchingPrevented = false;
  await rootListeners[0]({ target: button, preventDefault() { matchingPrevented = true; } });
  assert.equal(matchingPrevented, true);
  assert.equal(fetchCalls, 1);
  assert.deepEqual(payloads[0].entries, [["__RequestVerificationToken", "csrf-token"], ["Input.OsrsCharacterName", "Exact Character"]]);
  assert.equal(ehb.value, "42.75");
  assert.equal(password.value, "Password sentinel");
  assert.equal(confirmation.value, "Confirm sentinel");
  assert.equal(queried.some(selector => selector.includes("Password")), false, "the lookup must not inspect password fields");
})();

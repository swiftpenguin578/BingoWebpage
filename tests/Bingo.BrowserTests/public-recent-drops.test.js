const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

class Node {
  constructor({ tagName = "div", dataset = {}, href = null, children = [] } = {}) {
    this.tagName = tagName.toUpperCase();
    this.dataset = dataset;
    this.href = href;
    this.children = [];
    this.listeners = {};
    this.attributes = {};
    this.focused = false;
    this.value = "";
    this.hidden = false;
    this.textContent = "";
    children.forEach(child => this.append(child));
  }

  append(child) { child.parentElement = this; this.children.push(child); }
  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  dispatch(event) { return (this.listeners[event.type] ?? []).map(listener => listener(event)); }
  setAttribute(name, value) { this.attributes[name] = String(value); }
  removeAttribute(name) { delete this.attributes[name]; }
  focus(options) { this.focused = true; this.focusOptions = options; }
  replaceWith(replacement) {
    const index = this.parentElement.children.indexOf(this);
    this.parentElement.children.splice(index, 1, replacement);
    replacement.parentElement = this.parentElement;
  }
  closest(selector) {
    let current = this;
    while (current) {
      if (current.matches(selector)) return current;
      current = current.parentElement;
    }
    return null;
  }
  querySelector(selector) {
    if (this.matches(selector)) return this;
    for (const child of this.children) {
      const match = child.querySelector(selector);
      if (match) return match;
    }
    return null;
  }
  matches(selector) {
    const data = selector.match(/^\[data-([\w-]+)(?:="([^"]+)")?\]$/);
    if (!data) return false;
    const key = data[1].replace(/-([a-z])/g, (_match, character) => character.toUpperCase());
    return Object.hasOwn(this.dataset, key) && (data[2] === undefined || this.dataset[key] === data[2]);
  }
  cloneNode(deep) {
    return new Node({
      tagName: this.tagName,
      dataset: { ...this.dataset },
      href: this.href,
      children: deep ? this.children.map(child => child.cloneNode(true)) : []
    });
  }
}

const { initialize } = require("../../src/Bingo.Web/wwwroot/js/public-recent-drops.js");
const initialHref = "https://example.test/Events/test/Board?view=drops&dropCount=25";
const expandedHref = "https://example.test/Events/test/Board?view=drops&dropCount=50";

const link = (href, focus, target) => new Node({ tagName: "a", href, dataset: { publicRecentDropsNav: "", publicRecentDropsFocus: focus, publicRecentDropsFocusTarget: target } });
const region = (href, focus, target) => new Node({ dataset: { publicRecentDrops: "" }, children: [link(href, focus, target)] });
const root = new Node({ children: [region(expandedHref, "load", "load")] });
let parsedRegion = region(expandedHref, "load", "load");
let resolveFetch;
let fetchCalls = 0;
const window = {
  location: { href: initialHref, origin: "https://example.test", assign: value => { window.assigned = value; } },
  scrollX: 120,
  scrollY: 480,
  scrollToCalls: [],
  scrollTo: (x, y) => window.scrollToCalls.push([x, y]),
  requestAnimationFrame: callback => callback(),
  fetch: () => {
    fetchCalls++;
    return new Promise(resolve => { resolveFetch = resolve; });
  }
};
global.window = window;
global.document = { importNode: node => node.cloneNode(true) };
global.DOMParser = class { parseFromString() { return { querySelector: () => parsedRegion }; } };

initialize(root);
 (async () => {
  const firstClick = { type: "click", target: root.children[0].children[0], defaultPrevented: false, preventDefault() { this.defaultPrevented = true; } };
  const firstRequest = root.dispatch(firstClick)[0];
  const secondClick = { type: "click", target: firstClick.target, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; } };
  root.dispatch(secondClick);
  assert.equal(fetchCalls, 1, "duplicate recent-drops clicks share one request");
  resolveFetch({ ok: true, text: async () => "<html></html>" });
  await firstRequest;
  await Promise.resolve();
  const nextLoad = root.children[0].querySelector("[data-public-recent-drops-focus=\"load\"]");
  assert.equal(nextLoad.focused, true, "replacement keeps focus on the next Load action");
  assert.deepEqual(nextLoad.focusOptions, { preventScroll: true }, "replacement focus does not scroll the viewport");
  assert.deepEqual(window.scrollToCalls, [[120, 480], [120, 480]], "replacement restores the viewport before and after layout");

  const fallbackRoot = new Node({ children: [region(expandedHref, "load", "load")] });
  window.fetch = async () => ({ ok: false });
  const fallbackClick = { type: "click", target: fallbackRoot.children[0].children[0], defaultPrevented: false, preventDefault() { this.defaultPrevented = true; } };
  initialize(fallbackRoot);
  await fallbackRoot.dispatch(fallbackClick)[0];
  assert.equal(window.assigned, expandedHref, "failed enhancement follows the real navigation href");

  const filterSearch = new Node({ dataset: { publicRecentDropsSearch: "" } });
  filterSearch.value = "Player";
  const filterTeam = new Node({ dataset: { publicRecentDropsTeam: "" } });
  filterTeam.value = "";
  const filterClear = new Node({ dataset: { publicRecentDropsClear: "" } });
  const filterForm = new Node({ dataset: { publicRecentDropsFilterForm: "" }, children: [filterSearch, filterTeam, filterClear] });
  const filterResult = new Node({ dataset: { publicRecentDropsResult: "" } });
  const filterToolbar = new Node({ dataset: { publicRecentDropsToolbar: "" }, children: [filterForm, filterResult] });
  const filterRoot = new Node({ children: [filterToolbar, region(initialHref, "load", "load")] });
  let filterFetches = 0;
  let resolveFilterFetch;
  const filterUrls = [];
  window.location.href = initialHref;
  window.history = { replaceState: (_state, _title, value) => { window.replacedUrl = value; } };
  window.fetch = target => {
    filterFetches++;
    filterUrls.push(target);
    if (filterFetches === 1) {
      return new Promise(resolve => { resolveFilterFetch = resolve; });
    }
    return Promise.resolve({ ok: true, text: async () => "<html></html>" });
  };
  parsedRegion = region(initialHref, "load", "load");
  initialize(filterRoot);
  filterSearch.dispatch({ type: "input" });
  assert.equal(filterFetches, 0, "search waits for the debounce window");
  await new Promise(resolve => setTimeout(resolve, 270));
  assert.equal(filterFetches, 1, "debounced search fetches once");
  filterSearch.value = "Latest";
  filterSearch.dispatch({ type: "input" });
  await new Promise(resolve => setTimeout(resolve, 270));
  assert.equal(filterFetches, 1, "latest in-flight filter target waits for the current request");
  resolveFilterFetch({ ok: true, text: async () => "<html></html>" });
  for (let attempt = 0; attempt < 10 && filterFetches < 2; attempt++) await new Promise(resolve => setTimeout(resolve, 0));
  assert.equal(filterFetches, 2, "latest in-flight filter target is applied after the current request");
  assert.match(filterUrls[1], /dropCount=25/);
  assert.match(filterUrls[1], /dropSearch=Latest/);
  assert.match(window.replacedUrl, /dropSearch=Latest/);
  filterTeam.value = "team-one";
  await filterTeam.dispatch({ type: "change" })[0];
  assert.match(window.replacedUrl, /dropSearch=Latest/);
  assert.match(window.replacedUrl, /dropTeam=team-one/);
  filterSearch.value = "Stale timer";
  filterSearch.dispatch({ type: "input" });
  const beforeTeamTimerWait = filterFetches;
  await filterTeam.dispatch({ type: "change" })[0];
  await new Promise(resolve => setTimeout(resolve, 270));
  assert.equal(filterFetches, beforeTeamTimerWait + 1, "team change cancels the pending search debounce");
  filterSearch.value = "To clear";
  filterSearch.dispatch({ type: "input" });
  const beforeClearTimerWait = filterFetches;
  await filterClear.dispatch({ type: "click" })[0];
  const afterClear = filterFetches;
  await new Promise(resolve => setTimeout(resolve, 270));
  assert.equal(afterClear, beforeClearTimerWait + 1, "clear cancels the pending search debounce");
  assert.equal(filterSearch.value, "", "inline X clears only search");
  assert.equal(filterClear.hidden, true, "inline X hides after clearing");
  assert.match(window.replacedUrl, /dropTeam=team-one/);
  assert.doesNotMatch(window.replacedUrl, /dropSearch=/);

  const boardMarkup = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/Pages/Events/Board.cshtml"), "utf8");
  assert.match(boardMarkup, /var showSubmitDrop = Model\.Board\.SubmissionsOpen;/, "masthead uses the projected submission-open state");
  assert.match(boardMarkup, /public-ui-masthead-result[\s\S]*public-ui-masthead-rank[^>]*>#1<\/strong>/, "closed masthead uses the shared result callout and rank");
  assert.match(boardMarkup, /T\["Official result"\][\s\S]*T\["Provisional result"\]/, "masthead distinguishes provisional and official results");
  assert.match(boardMarkup, /class="public-ui-action public-ui-action--text public-ui-recent-drop-back" data-public-recent-drops-focus="back" href="#recent-drops-latest"/, "Back to latest is a fragment link");
  assert.doesNotMatch(boardMarkup.match(/class="public-ui-action public-ui-action--text public-ui-recent-drop-back"[^>]+>/)?.[0] ?? "", /data-public-recent-drops-nav/, "Back to latest is not a fetch navigation");
  const catalogueMarkup = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/Pages/Admin/PublicUi.cshtml"), "utf8");
  assert.match(catalogueMarkup, /In the lead[\s\S]*public-ui-masthead-rank[^>]*>#1<\/strong>[\s\S]*Provisional result/, "PublicUi demonstrates the result callout composition");
  assert.match(catalogueMarkup, /class="public-ui-action public-ui-action--text public-ui-recent-drop-back" href="#recent-drops-specimen-latest"/, "PublicUi Back to latest uses the specimen feed fragment");
  const siteCss = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/wwwroot/css/site.css"), "utf8");
  assert.match(siteCss, /\.public-event-dashboard \.public-ui-masthead-result \.public-ui-component-header > \.public-ui-masthead-rank \{[^}]*color: #d8b65d !important;/, "result rank uses the public leading-rank color");
  assert.doesNotMatch(boardMarkup, /public-ui-masthead-team-link|public-ui-action--hyperlink[\s\S]*eventResult\.TeamName/, "closed result team is not rendered as a hyperlink");
  assert.match(siteCss, /body\.public-board-page > \.container[\s\S]*?width: min\(calc\(100% - 1\.5rem\), 80rem\);[\s\S]*?max-width: none;/, "public Board content uses the fluid 80rem max-width container");
  assert.match(siteCss, /@media \(max-width: 1100px\) \{\s*\.public-ui-recent-drop-grid \{ grid-template-columns: 1fr; \}/, "Recent drops become one column before intermediate card collisions");
  assert.match(siteCss, /\.public-ui-recent-drop-card[^\n]*overflow: hidden;/, "Recent drop card content remains contained");
  assert.match(boardMarkup, /class="public-ui-recent-drop-progression"[^>]*title="@drop\.TileName progress">@drop\.ProgressAfter\/@drop\.Target<\/strong>/, "Recent drops render historical per-drop progress");
  assert.doesNotMatch(boardMarkup, /var tile = team\?\.Tiles\.FirstOrDefault/, "Recent drops do not read current tile progress for each card");
  assert.match(catalogueMarkup, /data-public-ui-recent-drops-toolbar/, "PublicUi defines the recent-drops browsing toolbar");
  assert.doesNotMatch(catalogueMarkup, />Browse drops<\//, "PublicUi does not render the obsolete visible toolbar label");
  assert.doesNotMatch(boardMarkup, />Browse drops<\//, "live toolbar does not render the obsolete visible toolbar label");
  assert.match(catalogueMarkup, /<label class="visually-hidden" for="public-ui-recent-drops-search">Search drops<\/label>/, "PublicUi keeps an accessible search label");
  assert.match(boardMarkup, /<label class="visually-hidden" for="public-recent-drops-search">Search drops<\/label>/, "live toolbar keeps an accessible search label");
  assert.match(catalogueMarkup, /type="search"[^>]+placeholder="Search drops, players, teams or tiles…"/, "toolbar uses the full search placeholder");
  assert.match(boardMarkup, /data-public-recent-drops-search[^>]+name="dropSearch"[^>]+type="search"[^>]+value="@Model\.DropSearch" placeholder="Search drops, players, teams or tiles…"/, "live toolbar uses the full search placeholder");
  assert.match(catalogueMarkup, /class="public-ui-recent-drops-result-count visually-hidden"[^>]+aria-live="polite"/, "PublicUi keeps the asynchronous result count announced but visually hidden");
  assert.match(boardMarkup, /class="public-ui-recent-drops-result-count visually-hidden"[^>]+data-public-recent-drops-result aria-live="polite"/, "live filtering keeps the asynchronous result count announced but visually hidden");
  assert.match(catalogueMarkup, /type="button" class="public-ui-recent-drops-search-clear"[^>]+aria-label="Clear search"/, "toolbar reset is an accessible button");
  assert.match(catalogueMarkup, /class="public-ui-recent-drops-filter-icon"/, "toolbar exposes a visible team filter icon");
  assert.match(catalogueMarkup, /class="public-ui-recent-drops-filter-label"[^>]*>Team:<\/label>/, "toolbar exposes the visible Team label");
  assert.match(catalogueMarkup, /class="public-ui-select public-ui-select--accent"/, "toolbar uses the distinct accent select variant");
  assert.match(catalogueMarkup, /class="public-ui-recent-drops-layout">\s*<div class="public-ui-recent-drops-main"[\s\S]*<div class="public-ui-recent-drops-sidebar">[\s\S]*<div class="public-ui-surface public-ui-surface--charcoal public-ui-recent-drops-toolbar"[^>]*data-public-ui-recent-drops-toolbar/, "PublicUi keeps the toolbar in the shared right-rail stack");
  assert.match(boardMarkup, /class="public-ui-recent-drops-layout">\s*<div class="public-ui-recent-drops-main"[\s\S]*<div class="public-ui-recent-drops-sidebar">[\s\S]*<form class="public-ui-surface public-ui-surface--charcoal public-ui-recent-drops-toolbar"[^>]*data-public-recent-drops-toolbar/, "live Board keeps the toolbar in the shared right-rail stack");
  assert.match(catalogueMarkup, /recent-drops-search[\s\S]*recent-drops-result-count[\s\S]*recent-drops-filter-group/, "PublicUi orders search, result count, then Team filter");
  assert.match(boardMarkup, /public-recent-drops-search[\s\S]*public-ui-recent-drops-result-count[\s\S]*public-ui-recent-drops-filter-group/, "live toolbar orders search, result count, then Team filter");
  assert.match(boardMarkup, /name="dropSearch"/, "live toolbar submits the search query");
  assert.match(boardMarkup, /name="dropTeam"/, "live toolbar submits the team query");
  const recentDropsScript = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/wwwroot/js/public-recent-drops.js"), "utf8");
  assert.match(recentDropsScript, /setTimeout\(\(\) => \{ debounceTimer = undefined; applyFilters\(\); \}, 250\)/, "live search uses a modest debounce");
  assert.match(recentDropsScript, /if \(pending\) \{[\s\S]*queuedFilterTarget = target;/, "latest filter target is queued during an in-flight request");
  assert.match(recentDropsScript, /cancelDebounce\(\); return applyFilters\(\)/, "immediate filter actions cancel stale debounce timers");
  assert.match(recentDropsScript, /target\.searchParams\.set\("dropCount", "25"\)/, "filter changes reset the visible count");
  assert.match(recentDropsScript, /history\?\.replaceState/, "filter navigation synchronizes the URL without a reload");
  assert.match(boardMarkup, /asp-route-dropSearch="@Model\.DropSearch" asp-route-dropTeam="@Model\.DropTeam"/, "pagination preserves active filters");
  assert.doesNotMatch(catalogueMarkup, /class="visually-hidden" for="public-ui-recent-drops-team"/, "toolbar does not hide the Team label");
  assert.match(catalogueMarkup, /clearButton\.hidden = input\.value\.length === 0/, "toolbar hides reset when search is empty");
  assert.match(catalogueMarkup, /input\.value = ""; syncClearButton\(\); input\.focus\(\)/, "toolbar reset clears only search and restores focus");
  assert.match(siteCss, /\.public-ui-recent-drops-search-input::-webkit-search-cancel-button/, "toolbar suppresses the native search cancellation icon");
  assert.match(siteCss, /\.public-ui-recent-drops-layout \{[^}]*grid-template-columns: minmax\(0, 3fr\) minmax\(12rem, 1fr\);/, "recent drops layout reserves a right sidebar");
  assert.match(siteCss, /\.public-ui-recent-drops-sidebar \{[^}]*grid-column: 2; grid-row: 1;[^}]*gap: 0\.85rem;/, "sidebar owns the stacked statistics and toolbar components");
  assert.match(siteCss, /\.public-ui-recent-drops-toolbar \{[^}]*padding: 0\.7rem 0\.8rem;/, "toolbar is the shared sidebar component below statistics");
  assert.doesNotMatch(siteCss, /\.public-ui-recent-drops-toolbar \{[^}]*border-top:/, "obsolete upper toolbar divider styling is removed");
  assert.match(siteCss, /\.public-ui-recent-drops-search \{[^}]*grid-column: 1 \/ -1;[^}]*max-width: none;/, "toolbar search spans the full inner width");
  assert.match(siteCss, /\.public-ui-recent-drops-filter-group \{[^}]*width: 100%;[^}]*grid-column: 1 \/ -1;[^}]*grid-row: 2;/, "filter row spans the component width");
  assert.match(siteCss, /\.public-ui-recent-drops-filter-wrap \{[^}]*margin-left: auto;/, "selector is anchored to the toolbar right edge");
  assert.match(siteCss, /\.public-ui-recent-drops-sidebar \{[^}]*position: sticky; top: 1rem;[^}]*margin-top: 1\.15rem;/, "desktop sidebar owns sticky behavior and divider-to-card alignment offset");
  assert.doesNotMatch(siteCss, /\.public-ui-recent-drop-stats \{[^}]*position: sticky;/, "stats card does not own a competing sticky position");
  assert.match(siteCss, /@media \(max-width: 900px\) \{[\s\S]*\.public-ui-recent-drops-sidebar \{ display: contents; position: static; margin-top: 0; \}[\s\S]*\.public-ui-recent-drops-sidebar > \.public-ui-surface \{ position: static; grid-column: 1; grid-row: auto; order: 1;[\s\S]*\.public-ui-recent-drops-toolbar \{ grid-column: 1; grid-row: auto; order: 2; \}[\s\S]*\.public-ui-recent-drops-main \{ grid-column: 1; grid-row: auto; order: 3; \}/, "responsive layout puts the sidebar cards and toolbar above the feed");
assert.match(siteCss, /@media \(max-width: 900px\) \{[\s\S]*\.public-ui-recent-drop-stats-list \{ grid-template-columns: repeat\(2, minmax\(0, 1fr\)\); column-gap: 0; \}/, "drop statistics use continuous paired-row dividers without a vertical separator");
assert.doesNotMatch(siteCss, /\.public-ui-recent-drop-stats-list > div:nth-child\(odd\):not\(:last-child\)::after/, "drop statistics have no vertical separators between paired cells");
assert.match(siteCss, /@media \(max-width: 390px\) \{[\s\S]*\.public-ui-recent-drop-stats-list \{ grid-template-columns: 1fr; \}[\s\S]*\.public-ui-recent-drop-stats-list > div:nth-child\(odd\):not\(:last-child\) \{ padding-right: 0; \}/, "drop statistics return to one column on small phones");
  assert.match(siteCss, /@media \(min-width: 391px\) and \(max-width: 900px\) \{[\s\S]*\.public-ui-recent-drop-stats-list > div:nth-child\(odd\):nth-last-child\(2\),[\s\S]*border-bottom: 0; \}/, "drop statistics remove the dangling divider beneath an even final pair");
  assert.match(siteCss, /\.public-ui-select--accent \{[^}]*min-height: 2rem;[^}]*padding: 0\.28rem 1\.8rem 0\.28rem 0\.65rem;[^}]*background: var\(--public-ui-data-blue\);[^}]*border: 0;/, "accent select is compact, blue-filled, and borderless");
  assert.doesNotMatch(siteCss, /\.public-ui-recent-drops-toolbar-label/, "obsolete visible toolbar label styling is removed");
  assert.match(siteCss, /\.public-ui-recent-drops-result-count \{[^}]*color: var\(--public-ui-text-muted\);/, "result count retains shared live-region styling");
  assert.match(siteCss, /@media \(max-width: 600px\) \{[\s\S]*\.public-ui-recent-drops-filter-group \{ grid-column: 1 \/ -1; grid-row: 2; \}/, "narrow toolbar keeps the filter row full width");
  assert.doesNotMatch(siteCss, /@media \(max-width: 600px\) \{[\s\S]*\.public-ui-recent-drops-result-count \{/, "narrow toolbar has no obsolete visible-count placement");
  assert.match(siteCss, /@media \(max-width: 600px\) \{[\s\S]*\.public-ui-recent-drops-search \{ grid-column: 1 \/ -1; grid-row: 1; max-width: none; \}/, "toolbar search returns to full width on narrow screens");
  assert.match(siteCss, /\.public-ui-select--accent:focus-visible \{[^}]*var\(--public-ui-data-blue\)/, "accent select keeps a visible keyboard focus ring");
  assert.match(siteCss, /\.public-ui-recent-drops-search-clear\[hidden\] \{ display: none; \}/, "toolbar reset has an explicit hidden state");
})().catch(error => {
  setImmediate(() => { throw error; });
});

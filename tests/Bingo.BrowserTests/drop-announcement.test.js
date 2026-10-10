const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "../..");
const script = fs.readFileSync(path.join(root, "src/Bingo.Web/wwwroot/js/drop-announcement.js"), "utf8");
const layout = fs.readFileSync(path.join(root, "src/Bingo.Web/Pages/Shared/_DropAnnouncement.cshtml"), "utf8");
const endpoint = fs.readFileSync(path.join(root, "src/Bingo.Web/Announcements/DropAnnouncementEndpoints.cs"), "utf8");
const evidence = fs.readFileSync(path.join(root, "src/Bingo.Web/wwwroot/js/public-evidence.js"), "utf8");
const drops = fs.readFileSync(path.join(root, "src/Bingo.Web/wwwroot/js/public-recent-drops.js"), "utf8");
const board = fs.readFileSync(path.join(root, "src/Bingo.Web/Pages/Events/Board.cshtml"), "utf8");
const boardModel = fs.readFileSync(path.join(root, "src/Bingo.Web/Pages/Events/Board.cshtml.cs"), "utf8");

assert.match(layout, /User\.Identity\?\.IsAuthenticated == true && accountId is not null/, "the public banner is available to authenticated Admins as well as participants");
assert.doesNotMatch(layout, /!admin/, "the shared public banner no longer excludes Admin accounts");
assert.match(script, /data-progress-event/);
assert.match(script, /data-stats-event/);
assert.match(script, /api\/drop-announcements\/current\?limit=100/, "pages without event context retain participant-scoped fallback");
assert.match(script, /eventId && response\.status === 404/, "an exact-event denial clears a stale banner snapshot");
assert.match(script, /oldId = current\(\)\?\.submissionId/);
assert.match(script, /queue\.some\(item => !oldIds\.has\(item\.submissionId\)\)/, "bursts preserve the selected item while detecting later arrivals");
assert.match(script, /dataset\.countdown = 'held'/, "focus and pointer interaction hold the local countdown");
assert.match(script, /beginCountdown\(\);/, "leaving interaction starts a fresh local countdown");
assert.match(script, /direction > 0 \? 'next' : 'previous'/, "manual navigation selects a directional transition");
assert.match(script, /const MOTION_MS = 320/);
assert.match(script, /const HEIGHT_MS = MOTION_MS \* 2/);
assert.match(script, /beginStateTransition\('compact', 'to-compact'\)/, "compaction keeps the rendered content through the prototype transition");
assert.match(script, /hideAnnouncement\(true\)/, "dismissal uses the delayed exit path");
assert.match(script, /sessionStorage\.setItem\(key\(\)/, "navigation state is transient and account/event scoped");
assert.match(script, /snapshotSequence/, "client reconciles the server snapshot boundary");
assert.match(script, /claimAndMaybeExpand[\s\S]*result\?\.claimed/, "automatic expansion is server claimed and cooldown protected");
assert.match(script, /oldState\.expanded[\s\S]*queue\.some\(item => !oldIds\.has/, "a burst does not steal an expanded selection");
assert.match(script, /encodeURIComponent\(snapshot\.eventSlug\).*submissionId/, "GO TO DROP preserves the selected submission deep link");
assert.match(layout, /Html\.AntiForgeryToken\(\)/, "mutations have an antiforgery token in the shared shell");
assert.match(layout, /data-progression-template/, "the kicker template is rendered through the shared localizer");
assert.match(layout, /data-clear-success/, "CLEAR ALL success copy is rendered through the shared localizer");
assert.match(script, /showBingoToast/, "CLEAR ALL uses the shared transient toast owner");
assert.match(endpoint, /RequireAuthorization\(\)/);
assert.match(endpoint, /DismissSnapshotAsync/);
assert.match(endpoint, /ValidateRequestAsync/);
assert.ok((endpoint.match(/ValidMutationAsync\(context, antiforgery\)/g) || []).length >= 6, "every mutation route validates antiforgery");
assert.match(endpoint, /GetNewAsync/);
assert.match(endpoint, /api\/public\/events\/\{slug\}\/recent-drops/);
assert.match(evidence, /public-evidence-opened/);
assert.match(evidence, /image\.addEventListener\("error"/);
assert.match(drops, /api\/drop-announcements\/new/);
assert.match(drops, /bingo-progress-changed/);
assert.match(drops, /const scrollY = windowObject\.scrollY/);
assert.match(drops, /api\/public\/events\/\$\{encodeURIComponent/);
assert.match(drops, /existingIds/);
assert.match(drops, /evidenceAltTemplate/);
assert.match(drops, /justNowLabel/);
assert.match(board, /data-evidence-alt-template/);
assert.match(board, /data-just-now-label/);
assert.match(boardModel, /Guid\? submissionId/);
assert.match(boardModel, /GetRecentDropAsync/);
assert.match(board, /data-evidence-open-on-load/);
assert.match(board, /data-evidence-submission-id/);
assert.match(board, /data-drop-clear-all/);

class FakeElement {
    constructor(children = {}) { this.children = children; this.childNodes = []; this.classList = { add() {}, remove() {} }; this.dataset = {}; this.listeners = {}; this.hidden = false; this.textContent = ""; this.disabled = false; this.href = ""; this.style = { height: "", removeProperty: property => { delete this.style[property]; } }; }
    querySelector(selector) { return this.children[selector] || null; }
    addEventListener(type, listener) { this.listeners[type] = listener; }
    dispatch(type, event = {}) { this.listeners[type]?.(event); }
    setAttribute(name, value) { this[name] = value; }
    contains(target) { return target === this; }
    append(...children) { this.childNodes.push(...children); this.firstElementChild ??= children[0]; }
    replaceChildren(...children) { this.childNodes = children; this.firstElementChild = children[0]; }
    getBoundingClientRect() { return { height: this.dataset.state === "compact" ? 50 : 100 }; }
    focus() { }
}

const token = new FakeElement();
token.value = "test-token";
const clearAllElement = new FakeElement();
clearAllElement.dataset.dropEventId = "event-a";
const rootElement = new FakeElement({
    '[data-drop-announcement-main]': new FakeElement(),
    '[data-drop-announcement-nav]': new FakeElement(),
    '[data-drop-announcement-previous]': new FakeElement(),
    '[data-drop-announcement-next]': new FakeElement(),
    '[data-drop-announcement-position]': new FakeElement(),
    '[data-drop-announcement-link]': new FakeElement(),
    '[data-drop-announcement-count]': new FakeElement(),
    '[data-drop-announcement-expand]': new FakeElement(),
    '[data-drop-announcement-dismiss]': new FakeElement(),
    'input[name="__RequestVerificationToken"]': token
});
rootElement.dataset.accountId = "account-a";
rootElement.dataset.progressionTemplate = "NEW TILE PROGRESSION: {0} / {1}";
rootElement.dataset.completedLabel = "TILE COMPLETED";
rootElement.dataset.positionTemplate = "{0} af {1}";
rootElement.dataset.clearSuccess = "All new marks cleared.";
const timers = [];
const session = new Map();
const windowListeners = {};
const requestLog = [];
let phase = "initial";
let activeSubmission = false;
let failAcknowledge = false;
let failClearAll = false;
let acknowledgementEvents = 0;
const toastLog = [];
let deferClaim = false;
let deferredClaimResolve;
const entry = (submissionId, completedTileAtApproval = false) => ({ submissionId, tileName: `Tile ${submissionId}`, tileArtworkReference: null, itemArtworkReference: null, teamName: "Team", playerName: "Player", completedTileAtApproval, progressAfter: 2, target: 5 });
const snapshots = {
    initial: { eventId: "event-a", eventSlug: "event-a", generation: 1, snapshotSequence: 1, queue: [entry("1")], queueTotalCount: 1 },
    burst: { eventId: "event-a", eventSlug: "event-a", generation: 1, snapshotSequence: 2, queue: [entry("2", true), entry("1")], queueTotalCount: 2 },
    late: { eventId: "event-a", eventSlug: "event-a", generation: 1, snapshotSequence: 3, queue: [entry("3"), entry("2"), entry("1")], queueTotalCount: 3 },
    blocked: { eventId: "event-a", eventSlug: "event-a", generation: 1, snapshotSequence: 4, queue: [entry("4"), entry("3"), entry("2"), entry("1")], queueTotalCount: 4 },
    deferred: { eventId: "event-a", eventSlug: "event-a", generation: 1, snapshotSequence: 5, queue: [entry("7"), entry("6"), entry("5"), entry("4"), entry("3"), entry("2"), entry("1")], queueTotalCount: 7 },
    visibleClaim: { eventId: "event-a", eventSlug: "event-a", generation: 1, snapshotSequence: 6, queue: [entry("6"), entry("5"), entry("4"), entry("3"), entry("2"), entry("1")], queueTotalCount: 6 }
};
const fakeWindow = {
    signalR: null,
    setTimeout(callback, delay) { const timer = { callback, delay, active: true }; timers.push(timer); return timer; },
    clearTimeout(timer) { if (timer) timer.active = false; },
    requestAnimationFrame(callback) { const timer = { callback, delay: 0, active: true }; timers.push(timer); return timer; },
    cancelAnimationFrame(timer) { if (timer) timer.active = false; },
    matchMedia() { return { matches: false }; },
    showBingoToast(message, type = "success") { toastLog.push({ message, type }); },
    addEventListener(type, listener) { windowListeners[type] = listener; },
    dispatchEvent(event) { if (event.type === "drop-announcement-acknowledged") acknowledgementEvents++; windowListeners[event.type]?.(event); }
};
const fakeDocument = {
    documentElement: { dataset: { postError: "Ændringen kunne ikke sendes. Kontrollér forbindelsen, og prøv igen." } },
    querySelector(selector) {
        if (selector === "[data-drop-clear-all]") return clearAllElement;
        return selector.includes("data-submission-drawer") || selector.includes("data-submission-result") ? (activeSubmission ? {} : null) : rootElement;
    },
    createElement() { return new FakeElement(); },
    createTextNode(text) { return { textContent: text }; },
    addEventListener() { }
};
const fakeFetch = async (url, options = {}) => {
    requestLog.push({ url, options });
    if (url.startsWith("/api/drop-announcements/current")) return { ok: true, status: 200, json: async () => snapshots[phase] };
    if (url.endsWith("/clear-all-new") && failClearAll) return { ok: false, status: 500, json: async () => null };
    if (url.endsWith("/claim")) {
        if (deferClaim) return { ok: true, status: 200, json: () => new Promise(resolve => { deferredClaimResolve = () => resolve({ claimed: true }); }) };
        return { ok: true, status: 200, json: async () => ({ claimed: phase === "initial" || phase === "visibleClaim" }) };
    }
    if (url.endsWith("/acknowledge-both") && failAcknowledge) return { ok: false, status: 500, json: async () => null };
    return { ok: true, status: 204, json: async () => null };
};
const context = { window: fakeWindow, document: fakeDocument, CustomEvent: class { constructor(type, init) { this.type = type; this.detail = init?.detail; } }, sessionStorage: { getItem: key => session.get(key) || null, setItem: (key, value) => session.set(key, value) }, fetch: fakeFetch, console };
require("node:vm").runInNewContext(script, context);
const flush = () => new Promise(resolve => setImmediate(resolve));
const runTimer = (delay = 6000) => { const timer = timers.find(item => item.active && item.delay === delay); assert.ok(timer, `expected an active ${delay}ms timer`); timer.active = false; timer.callback(); };

(async () => {
    await flush();
    await flush();
    runTimer(0);
    await flush();
    await flush();
    assert.equal(rootElement.dataset.state, "expanded", "an eligible first arrival opens after the atomic claim");
    assert.equal(rootElement.children['[data-drop-announcement-main]'].childNodes[0].childNodes.at(-1).childNodes[0].childNodes[0].textContent, "NEW TILE PROGRESSION: 2 / 5", "English progression kicker interpolates the approved progress values");
    rootElement.dispatch("pointerdown");
    assert.equal(rootElement.dataset.countdown, "held", "clicking inside holds the countdown");
    rootElement.dispatch("pointerleave");
    assert.equal(rootElement.dataset.countdown, "running", "leaving starts a fresh countdown");
    phase = "burst";
    rootElement.dataset.progressionTemplate = "NY TILEFREMGANG: {0} / {1}";
    fakeWindow.dispatchEvent({ type: "bingo-progress-changed" });
    await flush();
    await flush();
    assert.equal(rootElement.dataset.state, "expanded", "a burst keeps the banner expanded");
    assert.match(session.get("bingo:drop-announcement:account-a:event-a"), /\"selectedId\":\"1\"/, "a burst preserves the current selection");
    assert.equal(rootElement.children['[data-drop-announcement-position]'].textContent, "2 af 2", "the rendered position template is localized before client interpolation");
    rootElement.children['[data-drop-announcement-previous]'].dispatch("click");
    assert.equal(rootElement.children['[data-drop-announcement-main]'].childNodes[1].childNodes.at(-1).childNodes[0].childNodes[0].textContent, "TILE COMPLETED", "completed kickers stay counter-free and use the completion label");
    assert.equal(rootElement.children['[data-drop-announcement-main]'].childNodes.length, 2, "directional navigation keeps outgoing and incoming slides during the 320ms exit");
    assert.equal(rootElement.children['[data-drop-announcement-main]'].childNodes[1].dataset.direction, "previous");
    rootElement.children['[data-drop-announcement-next]'].dispatch("click");
    assert.equal(rootElement.children['[data-drop-announcement-main]'].childNodes[1].dataset.direction, "next", "a rapid reversal replaces the interrupted slide with the current direction");
    rootElement.dispatch("focusin");
    assert.equal(rootElement.dataset.countdown, "held", "keyboard focus holds the countdown");
    rootElement.dispatch("focusout", { relatedTarget: {} });
    assert.equal(rootElement.dataset.countdown, "running", "focus leaving starts a fresh countdown");
    runTimer();
    assert.equal(rootElement.dataset.state, "expanded", "compaction keeps the expanded content rendered during the outgoing phase");
    runTimer(320);
    assert.equal(rootElement.dataset.state, "compact", "the ten-second countdown compacts the banner");
    phase = "late";
    fakeWindow.dispatchEvent({ type: "bingo-progress-changed" });
    await flush();
    await flush();
    assert.equal(rootElement.dataset.state, "compact", "a cooldown-blocked later arrival stays compact");
    phase = "visibleClaim";
    fakeWindow.dispatchEvent({ type: "bingo-progress-changed" });
    await flush();
    await flush();
    assert.equal(rootElement.dataset.state, "compact", "a successful claim from a visible compact banner starts its outgoing phase");
    assert.equal(rootElement.dataset.transition, "to-expanded-out", "a visible compact claim uses the prototype measured expansion transition");
    runTimer(320);
    assert.equal(rootElement.dataset.state, "expanded", "the visible compact claim settles expanded after 320ms");
    runTimer();
    assert.equal(rootElement.dataset.state, "expanded", "the second compaction keeps expanded content through its outgoing phase");
    runTimer(320);
    assert.equal(rootElement.dataset.state, "compact");
    deferClaim = true;
    phase = "deferred";
    fakeWindow.dispatchEvent({ type: "bingo-progress-changed" });
    await flush();
    await flush();
    assert.equal(typeof deferredClaimResolve, "function", "the deferred automatic expansion claim has started");
    activeSubmission = true;
    deferredClaimResolve();
    await flush();
    assert.equal(rootElement.dataset.state, "compact", "a drawer opened during the claim prevents the response from expanding the banner");
    deferClaim = false;
    const claimCount = requestLog.filter(request => request.url.endsWith("/claim")).length;
    activeSubmission = true;
    phase = "blocked";
    fakeWindow.dispatchEvent({ type: "bingo-progress-changed" });
    await flush();
    await flush();
    assert.equal(rootElement.dataset.state, "compact", "an active submission keeps an arriving banner compact");
    assert.equal(requestLog.filter(request => request.url.endsWith("/claim")).length, claimCount, "an active submission suppresses automatic expansion claims");
    activeSubmission = false;
    rootElement.children['[data-drop-announcement-expand]'].dispatch("click");
    runTimer(320);
    assert.equal(rootElement.dataset.state, "expanded", "manual expansion remains available after the drawer state clears");
    assert.ok(requestLog.filter(request => request.options.method === "POST").every(request => request.options.headers.RequestVerificationToken === "test-token"), "mutations carry antiforgery protection");
    const beforePopupAck = requestLog.filter(request => request.url.endsWith("/acknowledge-both")).length;
    fakeWindow.dispatchEvent({ type: "public-evidence-opened", detail: { eventId: "event-a", submissionId: "1" } });
    await flush();
    assert.equal(requestLog.filter(request => request.url.endsWith("/acknowledge-both")).length, beforePopupAck + 1, "a successfully opened evidence popup acknowledges both states");
    assert.equal(acknowledgementEvents, 1, "a successful acknowledgement announces the Drops badge reconciliation");
    const afterPopupAck = requestLog.filter(request => request.url.endsWith("/acknowledge-both")).length;
    failAcknowledge = true;
    fakeWindow.dispatchEvent({ type: "public-evidence-opened", detail: { eventId: "event-a", submissionId: "2" } });
    await flush();
    assert.equal(requestLog.filter(request => request.url.endsWith("/acknowledge-both")).length, afterPopupAck + 1, "a failed acknowledgement still attempts the server mutation");
    assert.equal(acknowledgementEvents, 1, "a failed acknowledgement emits no badge reconciliation");
    await flush();
    assert.equal(acknowledgementEvents, 1, "a failed popup load emits no acknowledgement event");
    failAcknowledge = false;
    clearAllElement.dispatch("click");
    await flush();
    assert.equal(acknowledgementEvents, 2, "CLEAR ALL success announces reconciliation for loaded badges");
    assert.deepEqual(toastLog.at(-1), { message: "All new marks cleared.", type: "success" }, "CLEAR ALL success uses the localized shared toast owner");
    rootElement.dataset.clearSuccess = "Alle ny-markeringer er ryddet.";
    clearAllElement.dispatch("click");
    await flush();
    assert.deepEqual(toastLog.at(-1), { message: "Alle ny-markeringer er ryddet.", type: "success" }, "CLEAR ALL success supports the Danish localized copy");
    failClearAll = true;
    const toastCountBeforeFailedClear = toastLog.length;
    clearAllElement.dispatch("click");
    await flush();
    assert.equal(toastLog.length, toastCountBeforeFailedClear + 1, "failed CLEAR ALL reports one shared error toast");
    assert.deepEqual(toastLog.at(-1), { message: "Ændringen kunne ikke sendes. Kontrollér forbindelsen, og prøv igen.", type: "error" }, "failed CLEAR ALL never reports success");
    failClearAll = false;
    rootElement.children['[data-drop-announcement-dismiss]'].dispatch("click");
    await flush();
    assert.equal(rootElement.hidden, false, "dismissal retains the rendered banner during its exit");
    runTimer(340);
    assert.equal(rootElement.hidden, true, "dismissal hides after the 320ms exit plus settling delay");
    phase = "deferred";
    fakeWindow.dispatchEvent({ type: "bingo-progress-changed" });
    await flush();
    await flush();
    rootElement.children['[data-drop-announcement-expand]'].dispatch("click");
    runTimer(320);
    assert.equal(rootElement.dataset.state, "expanded", "manual expansion restores the measured expanded state");
    rootElement.children['[data-drop-announcement-dismiss]'].dispatch("click");
    await flush();
    assert.equal(rootElement.hidden, false, "expanded dismissal retains the rendered content during its exit");
    runTimer(340);
    assert.equal(rootElement.hidden, true, "expanded dismissal also waits for the prototype exit to settle");
})().catch(error => { setImmediate(() => { throw error; }); });

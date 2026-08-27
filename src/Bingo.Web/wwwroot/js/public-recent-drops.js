(function () {
  function initialize(root = document, windowObject = window, documentObject = document) {
    const region = root.querySelector("[data-public-recent-drops]");
    if (!region) return;

    const toolbar = root.querySelector("[data-public-recent-drops-toolbar]");
    const form = toolbar?.matches?.("[data-public-recent-drops-filter-form]")
      ? toolbar
      : toolbar?.querySelector("[data-public-recent-drops-filter-form]");
    const search = toolbar?.querySelector("[data-public-recent-drops-search]");
    const team = toolbar?.querySelector("[data-public-recent-drops-team]");
    const clear = toolbar?.querySelector("[data-public-recent-drops-clear]");
    let pending = false;
    let debounceTimer;
    let queuedFilterTarget;

    const cancelDebounce = () => {
      clearTimeout(debounceTimer);
      debounceTimer = undefined;
    };

    const syncClear = () => {
      if (search && clear) clear.hidden = search.value.length === 0;
    };

    const filterUrl = () => {
      const target = new URL(windowObject.location.href);
      target.searchParams.set("view", "drops");
      target.searchParams.set("dropCount", "25");
      const searchValue = search?.value.trim() || "";
      const teamValue = team?.value || "";
      if (searchValue) target.searchParams.set("dropSearch", searchValue);
      else target.searchParams.delete("dropSearch");
      if (teamValue) target.searchParams.set("dropTeam", teamValue);
      else target.searchParams.delete("dropTeam");
      return target;
    };

    const replaceFeed = async (target, trigger = null) => {
      const currentRegion = root.querySelector("[data-public-recent-drops]");
      if (!currentRegion) return;
      if (pending) {
        if (!trigger) queuedFilterTarget = target;
        return;
      }

      const scrollX = windowObject.scrollX;
      const scrollY = windowObject.scrollY;
      pending = true;
      currentRegion.setAttribute("aria-busy", "true");
      trigger?.setAttribute("aria-disabled", "true");

      try {
        const response = await windowObject.fetch(target.href, {
          credentials: "same-origin",
          headers: { Accept: "text/html", "X-Requested-With": "XMLHttpRequest" }
        });
        if (!response.ok) throw new Error("Recent drops request failed.");

        const parsed = new DOMParser().parseFromString(await response.text(), "text/html");
        const nextRegion = parsed.querySelector("[data-public-recent-drops]");
        if (!nextRegion) throw new Error("Recent drops region was not returned.");

        const replacement = documentObject.importNode(nextRegion, true);
        currentRegion.replaceWith(replacement);
        const nextResult = parsed.querySelector("[data-public-recent-drops-result]");
        const result = root.querySelector("[data-public-recent-drops-result]");
        if (nextResult && result) result.textContent = nextResult.textContent;
        windowObject.history?.replaceState?.({}, "", target.href);

        const focusKey = trigger?.dataset.publicRecentDropsFocusTarget;
        const focusTarget = focusKey
          ? replacement.querySelector(`[data-public-recent-drops-focus="${focusKey}"]`)
          : null;
        const fallbackFocusTarget = replacement.querySelector("[data-public-recent-drops-focus=\"back\"]") || replacement;
        if (focusTarget || trigger) (focusTarget || fallbackFocusTarget).focus?.({ preventScroll: true });
        const restoreScroll = () => windowObject.scrollTo?.(scrollX, scrollY);
        restoreScroll();
        windowObject.requestAnimationFrame?.(restoreScroll);
      } catch {
        if (!queuedFilterTarget) windowObject.location.assign(target.href);
      } finally {
        currentRegion.removeAttribute("aria-busy");
        root.querySelector("[data-public-recent-drops]")?.removeAttribute("aria-busy");
        trigger?.removeAttribute("aria-disabled");
        pending = false;
        const queuedTarget = queuedFilterTarget;
        queuedFilterTarget = undefined;
        if (queuedTarget) replaceFeed(queuedTarget);
      }
    };

    const applyFilters = () => replaceFeed(filterUrl());
    if (form) form.addEventListener("submit", event => { event.preventDefault(); cancelDebounce(); return applyFilters(); });
    if (search) {
      search.addEventListener("input", () => {
        syncClear();
        cancelDebounce();
        debounceTimer = setTimeout(() => { debounceTimer = undefined; applyFilters(); }, 250);
      });
    }
    if (team) team.addEventListener("change", () => { cancelDebounce(); return applyFilters(); });
    if (clear) clear.addEventListener("click", () => {
      if (!search) return;
      cancelDebounce();
      search.value = "";
      syncClear();
      search.focus({ preventScroll: true });
      return applyFilters();
    });
    syncClear();

    root.addEventListener("click", event => {
      const link = event.target.closest?.("[data-public-recent-drops-nav]");
      if (!link || event.defaultPrevented || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

      const currentRegion = link.closest("[data-public-recent-drops]");
      if (!currentRegion) return;
      const target = new URL(link.href || link.getAttribute("href"), windowObject.location.href);
      if (target.origin !== windowObject.location.origin) return;

      event.preventDefault();
      replaceFeed(target, link);
    });
  }

  const api = { initialize };
  if (typeof window !== "undefined") {
    window.publicRecentDrops = api;
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", () => initialize());
    else initialize();
  }
  if (typeof module !== "undefined" && module.exports) module.exports = api;
})();

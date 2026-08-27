(function (root, windowObject) {
  function initialize(scope = root, win = windowObject) {
    const form = scope.querySelector?.("[data-captain-ledger-form]");
    let results = scope.querySelector?.("[data-captain-ledger-results]");
    const search = form?.querySelector?.("[data-captain-ledger-search]");
    const player = form?.querySelector?.("[data-captain-ledger-player]");
    if (!form || !results || !search || !player || form.dataset.captainLedgerInitialized === "true") return;

    form.dataset.captainLedgerInitialized = "true";
    let debounceId;
    let activeController;

    const initializeSorting = () => {
      const table = results.querySelector?.("[data-public-leaderboard-sort-table]");
      const sortControls = table ? [...table.querySelectorAll("[data-public-leaderboard-sort-control]")] : [];
      const sortRows = table ? [...table.querySelectorAll("[data-public-leaderboard-sort-row]")] : [];
      const body = table?.querySelector?.("tbody");
      if (!table || sortControls.length === 0 || sortRows.length === 0 || !body || table.dataset.captainLedgerSortInitialized === "true") return;

      table.dataset.captainLedgerSortInitialized = "true";
      const originalOrder = new Map(sortRows.map((row, index) => [row, index]));
      let activeKey;
      let activeDirection;
      const datasetKey = key => `sort${key.split("-").map(part => `${part[0].toUpperCase()}${part.slice(1)}`).join("")}`;
      const setSortState = () => sortControls.forEach(control => {
        const active = control.dataset.publicLeaderboardSortKey === activeKey;
        const header = control.parentElement;
        if (header) header.setAttribute("aria-sort", active ? activeDirection : "none");
        control.classList.toggle("is-sorted", active);
        control.classList.toggle("is-ascending", active && activeDirection === "ascending");
        control.classList.toggle("is-descending", active && activeDirection === "descending");
        const labels = scope.ownerDocument?.documentElement?.dataset ?? {};
        const label = control.dataset.publicLeaderboardSortLabel || labels.sortColumn || "";
        const direction = activeDirection === "ascending" ? labels.sortDirectionAscending : labels.sortDirectionDescending;
        const nextDirection = activeDirection === "ascending" ? labels.sortDirectionDescending : labels.sortDirectionAscending;
        control.setAttribute("aria-label", active
          ? (labels.sortCurrentTemplate || "").replace("{0}", label).replace("{1}", direction || "").replace("{2}", nextDirection || "")
          : (labels.sortByTemplate || "").replace("{0}", label));
      });
      sortControls.forEach(control => control.addEventListener("click", event => {
        event.preventDefault();
        const key = control.dataset.publicLeaderboardSortKey;
        const numeric = control.dataset.publicLeaderboardSortType === "number";
        const defaultDirection = numeric ? "descending" : "ascending";
        activeDirection = key === activeKey ? activeDirection === "ascending" ? "descending" : "ascending" : defaultDirection;
        activeKey = key;
        const property = datasetKey(key);
        sortRows.sort((left, right) => {
          const leftValue = left.dataset[property] ?? "";
          const rightValue = right.dataset[property] ?? "";
          const leftUnavailable = numeric && leftValue === "";
          const rightUnavailable = numeric && rightValue === "";
          if (leftUnavailable !== rightUnavailable) return leftUnavailable ? 1 : -1;
          const comparison = numeric ? Number(leftValue) - Number(rightValue) : leftValue.localeCompare(rightValue, undefined, { sensitivity: "base" });
          return comparison === 0 ? originalOrder.get(left) - originalOrder.get(right) : activeDirection === "ascending" ? comparison : -comparison;
        });
        sortRows.forEach(row => body.appendChild(row));
        setSortState();
      }));
      setSortState();
    };

    initializeSorting();

    const displayUrl = url => {
      const next = new URL(url, win.location.href);
      next.searchParams.delete("handler");
      return `${next.pathname}${next.search}${next.hash}`;
    };
    const requestUrl = (page, href = null) => {
      const url = new URL(href ?? form.action ?? win.location.href, win.location.href);
      url.searchParams.set("handler", "Ledger");
      if (!href) {
        url.searchParams.set("eventId", form.elements.eventId.value);
        url.searchParams.set("teamId", form.elements.teamId.value);
        url.searchParams.set("ledgerPage", String(page));
        for (const [name, value] of [["search", search.value.trim()], ["player", player.value]]) {
          if (value) url.searchParams.set(name, value); else url.searchParams.delete(name);
        }
      }
      return url;
    };
    const replace = async (page = 1, href = null) => {
      const target = requestUrl(page, href);
      activeController?.abort();
      const controller = new AbortController();
      activeController = controller;
      results.setAttribute("aria-busy", "true");
      try {
        const response = await win.fetch(target, { credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" }, signal: controller.signal });
        if (!response.ok) throw new Error(form.dataset.captainLedgerRequestError || "");
        const parsed = new win.DOMParser().parseFromString(await response.text(), "text/html");
        const replacement = parsed.querySelector("[data-captain-ledger-results]");
        if (!replacement) throw new Error(form.dataset.captainLedgerResultsError || "");
        if (activeController !== controller) return;
        const imported = scope.importNode ? scope.importNode(replacement, true) : scope.ownerDocument?.importNode ? scope.ownerDocument.importNode(replacement, true) : replacement;
        results.replaceWith(imported);
        results = imported;
        initializeSorting();
        win.history.replaceState(win.history.state, "", displayUrl(target));
      } catch (error) {
        if (error?.name !== "AbortError") win.location.assign(displayUrl(target));
      } finally {
        if (activeController === controller) {
          activeController = null;
          results.removeAttribute("aria-busy");
        }
      }
    };

    search.addEventListener("input", () => {
      activeController?.abort();
      win.clearTimeout(debounceId);
      debounceId = win.setTimeout(() => replace(1), 220);
    });
    const clear = form.querySelector?.("[data-captain-ledger-clear]");
    const syncClear = () => { if (clear) clear.hidden = search.value.length === 0; };
    clear?.addEventListener("click", () => {
      win.clearTimeout(debounceId);
      search.value = "";
      syncClear();
      search.focus?.({ preventScroll: true });
      replace(1);
    });
    player.addEventListener("change", () => replace(1));
    form.addEventListener("submit", event => { event.preventDefault(); win.clearTimeout(debounceId); replace(1); });
    search.addEventListener("input", syncClear);
    syncClear();
    scope.addEventListener("click", event => {
      const link = event.target?.closest?.("[data-captain-ledger-page]");
      if (!link || !results.contains(link)) return;
      event.preventDefault();
      replace(new URL(link.href, win.location.href).searchParams.get("ledgerPage") || 1, link.href);
    });
  }

  if (typeof module !== "undefined") module.exports = { initialize };
  if (root?.addEventListener) root.addEventListener("DOMContentLoaded", () => initialize());
})(typeof document === "undefined" ? null : document, typeof window === "undefined" ? null : window);

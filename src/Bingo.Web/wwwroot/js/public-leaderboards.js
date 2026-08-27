(function () {
  const initializedRoots = new WeakSet();
  let selectDropdownId = 0;

  function enhanceSelectDropdown(select) {
    const parent = select.parentElement;
    const ownerDocument = select.ownerDocument;
    if (!parent || !ownerDocument) return;

    const existingChevron = select.nextElementSibling;
    if (existingChevron?.matches?.(".public-ui-select-chevron")) existingChevron.remove();

    const dropdown = ownerDocument.createElement("details");
    dropdown.className = "public-ui-compact-dropdown";
    dropdown.dataset.publicUiCompactDropdown = "";
    dropdown.dataset.publicUiSelectDropdown = "";
    dropdown.dataset.publicUiSelectEnhanced = "";

    const summary = ownerDocument.createElement("summary");
    summary.className = "public-ui-compact-dropdown-trigger";
    summary.setAttribute("aria-haspopup", "listbox");
    summary.setAttribute("aria-expanded", "false");

    const label = ownerDocument.createElement("span");
    label.dataset.publicUiDropdownLabel = "";
    const chevron = ownerDocument.createElement("span");
    chevron.className = "public-ui-select-chevron";
    chevron.setAttribute("aria-hidden", "true");
    summary.append(label, chevron);

    const menu = ownerDocument.createElement("div");
    menu.className = "public-ui-compact-dropdown-menu";
    menu.id = `public-ui-select-dropdown-${++selectDropdownId}`;
    menu.setAttribute("role", "listbox");
    const localized = ownerDocument.documentElement?.dataset ?? {};
    menu.setAttribute("aria-label", select.getAttribute("aria-label") || localized.publicSelectOption || "");
    menu.tabIndex = -1;
    summary.setAttribute("aria-controls", menu.id);

    const addOption = (option, container = menu, groupDisabled = false) => {
      const button = ownerDocument.createElement("button");
      button.type = "button";
      button.className = "public-ui-compact-dropdown-option";
      button.dataset.publicUiDropdownOption = option.value;
      button.dataset.publicUiDropdownLabel = option.textContent.trim();
      button.setAttribute("role", "option");
      button.setAttribute("aria-selected", String(option.selected));
      button.disabled = option.disabled || groupDisabled;

      const text = ownerDocument.createElement("span");
      text.textContent = option.textContent.trim();
      const check = ownerDocument.createElementNS("http://www.w3.org/2000/svg", "svg");
      check.dataset.publicUiDropdownCheck = "";
      check.setAttribute("aria-hidden", "true");
      check.setAttribute("viewBox", "0 0 24 24");
      check.setAttribute("fill", "none");
      check.setAttribute("stroke", "currentColor");
      check.setAttribute("stroke-linecap", "round");
      check.setAttribute("stroke-linejoin", "round");
      check.setAttribute("stroke-width", "2");
      check.hidden = !option.selected;
      const path = ownerDocument.createElementNS("http://www.w3.org/2000/svg", "path");
      path.setAttribute("d", "m5 12 4 4L19 6");
      check.append(path);
      button.append(text, check);
      container.append(button);
    };

    Array.from(select.children).forEach(child => {
      if (child.tagName === "OPTGROUP") {
        const group = ownerDocument.createElement("div");
        group.setAttribute("role", "group");
        group.setAttribute("aria-label", child.label);
        Array.from(child.children).forEach(option => addOption(option, group, child.disabled));
        menu.append(group);
      } else if (child.tagName === "OPTION") {
        addOption(child);
      }
    });

    label.textContent = select.selectedOptions[0]?.textContent.trim() || localized.publicSelectOption || "";
    parent.insertBefore(dropdown, select);
    dropdown.append(summary, select, menu);
  }

  function enhanceSelectDropdowns(root) {
    root.querySelectorAll("select.public-ui-control-visual--select:not([data-public-ui-select-enhanced])")
      .forEach(select => {
        if (select.closest(".landing-shell-header")) return;
        enhanceSelectDropdown(select);
      });
  }

  function initialize(root = document, windowObject = window) {
    if (initializedRoots.has(root)) return;
    initializedRoots.add(root);

    enhanceSelectDropdowns(root);

    const details = [...root.querySelectorAll("[data-public-leaderboard-details]")];
    const controls = [...root.querySelectorAll("[data-public-leaderboard-expand-control]")];
    const detailsById = new Map(details.map(detail => [detail.id, detail]));
    const pairs = controls.map(control => [detailsById.get(control.getAttribute("aria-controls")), control]);
    const validPairs = pairs.filter(([detail]) => detail);
    if (validPairs.length > 0) {
      root.querySelectorAll("[data-public-leaderboard-expand-heading], [data-public-leaderboard-expand-cell]").forEach(element => element.removeAttribute("hidden"));
      root.querySelectorAll("[data-public-leaderboard-table]").forEach(table => table.setAttribute("data-public-leaderboard-enhanced", "true"));
      validPairs.forEach(([detail, control]) => {
        const summary = detail.querySelector("summary");
        if (!summary) return;

        const sync = () => {
          control.setAttribute("aria-expanded", String(detail.open));
          control.classList.toggle("is-expanded", detail.open);
        };
        summary.setAttribute("tabindex", "-1");
        summary.setAttribute("aria-disabled", "true");
        summary.addEventListener("click", event => event.preventDefault());
        summary.addEventListener("keydown", event => {
          if (event.key === "Enter" || event.key === " ") event.preventDefault();
        });
        detail.addEventListener("toggle", sync);
        control.removeAttribute("hidden");
        control.addEventListener("click", event => {
          event.preventDefault();
          detail.open = !detail.open;
          sync();
        });
        sync();
      });
    }

    root.querySelectorAll("[data-public-leaderboard-sort-table]").forEach(table => {
      const sortControls = [...table.querySelectorAll("[data-public-leaderboard-sort-control]")];
      const sortRows = [...table.querySelectorAll("[data-public-leaderboard-sort-row]")];
      const body = table.querySelector("tbody");
      if (sortControls.length === 0 || sortRows.length === 0 || !body) return;

      const originalOrder = new Map(sortRows.map((row, index) => [row, index]));
      const pagination = table.dataset.publicLeaderboardPlayersTable !== undefined
        ? table.parentElement?.querySelector("[data-public-leaderboard-pagination]")
        : null;
      const pageLabel = pagination?.querySelector("[data-public-leaderboard-page-label]");
      const pageControls = pagination ? [...pagination.querySelectorAll("[data-public-leaderboard-page-control]")] : [];
      const pageCount = Math.max(1, Math.ceil(sortRows.length / 20));
      let currentPage = 1;
      let activeKey = null;
      let activeDirection = null;
      const datasetKey = key => `sort${key.split("-").map(part => `${part[0].toUpperCase()}${part.slice(1)}`).join("")}`;
      const renderPage = () => {
        if (!pagination) return;
        const firstRow = (currentPage - 1) * 20;
        sortRows.forEach((row, index) => { row.hidden = index < firstRow || index >= firstRow + 20; });
        if (pageLabel) pageLabel.textContent = (table.ownerDocument?.documentElement?.dataset.publicPageTemplate || "").replace("{0}", currentPage).replace("{1}", pageCount);
        pagination.hidden = pageCount <= 1;
        pageControls.forEach(control => {
          const page = control.dataset.publicLeaderboardPageControl;
          const disabled = page === "first" || page === "previous" ? currentPage === 1 : currentPage === pageCount;
          control.disabled = disabled;
          control.setAttribute("aria-disabled", String(disabled));
          if (disabled) control.setAttribute("disabled", "");
          else control.removeAttribute("disabled");
        });
      };
      const setSortState = () => {
        sortControls.forEach(control => {
          const key = control.dataset.publicLeaderboardSortKey;
          const active = key === activeKey;
          const header = control.parentElement;
          if (header) header.setAttribute("aria-sort", active ? activeDirection : "none");
          control.classList.toggle("is-sorted", active);
          control.classList.toggle("is-ascending", active && activeDirection === "ascending");
          control.classList.toggle("is-descending", active && activeDirection === "descending");
          const label = control.dataset.publicLeaderboardSortLabel || "";
          const localized = table.ownerDocument?.documentElement?.dataset ?? {};
          const direction = activeDirection === "ascending" ? localized.publicSortAscending || "" : localized.publicSortDescending || "";
          const ariaLabel = active
            ? (localized.publicSortCurrentTemplate || "").replace("{0}", label).replace("{1}", direction).replace("{2}", label)
            : (localized.publicSortTemplate || "").replace("{0}", label);
          control.setAttribute("aria-label", ariaLabel);
        });
      };

      sortControls.forEach(control => control.addEventListener("click", () => {
        const key = control.dataset.publicLeaderboardSortKey;
        const numeric = control.dataset.publicLeaderboardSortType === "number";
        const defaultDirection = key === "rank" ? "ascending" : numeric ? "descending" : "ascending";
        activeDirection = key === activeKey
          ? activeDirection === "ascending" ? "descending" : "ascending"
          : defaultDirection;
        activeKey = key;
          const property = datasetKey(key);
          sortRows.sort((left, right) => {
            const leftValue = left.dataset[property] ?? "";
            const rightValue = right.dataset[property] ?? "";
            const leftUnavailable = numeric && leftValue === "";
            const rightUnavailable = numeric && rightValue === "";
            if (leftUnavailable !== rightUnavailable) return leftUnavailable ? 1 : -1;
            const comparison = numeric
              ? Number(leftValue) - Number(rightValue)
            : leftValue.localeCompare(rightValue, undefined, { sensitivity: "base" });
          return comparison === 0
            ? originalOrder.get(left) - originalOrder.get(right)
            : activeDirection === "ascending" ? comparison : -comparison;
        });
        sortRows.forEach(row => body.appendChild(row));
        currentPage = 1;
        setSortState();
        renderPage();
      }));
      pageControls.forEach(control => control.addEventListener("click", () => {
        if (control.disabled) return;
        switch (control.dataset.publicLeaderboardPageControl) {
          case "first": currentPage = 1; break;
          case "previous": currentPage = Math.max(1, currentPage - 1); break;
          case "next": currentPage = Math.min(pageCount, currentPage + 1); break;
          case "last": currentPage = pageCount; break;
        }
        renderPage();
      }));
      setSortState();
      renderPage();
    });

    const compactDropdownHandlers = new Map();
    const compactDropdowns = [...root.querySelectorAll("[data-public-ui-compact-dropdown]")];
    const syncCompactDropdown = dropdown => {
      const trigger = dropdown.querySelector("summary");
      if (!trigger) return;
      trigger.setAttribute("aria-expanded", String(dropdown.open));
    };
    const setCompactDropdownValue = (dropdown, value) => {
      const options = [...dropdown.querySelectorAll("[data-public-ui-dropdown-option]")];
      const option = options.find(candidate => candidate.dataset.publicUiDropdownOption === value) ?? options[0];
      if (!option) return;
      const active = option.dataset.publicUiDropdownOption;
      dropdown.dataset.publicUiDropdownValue = active;
      const label = dropdown.querySelector("[data-public-ui-dropdown-label]");
      if (label) label.textContent = option.dataset.publicUiDropdownLabel || active;
      options.forEach(candidate => {
        const selected = candidate === option;
        candidate.setAttribute("aria-selected", String(selected));
        const check = candidate.querySelector("[data-public-ui-dropdown-check]");
        if (check) {
          if (selected) check.removeAttribute("hidden");
          else check.setAttribute("hidden", "");
        }
      });
    };
    const closeCompactDropdown = (dropdown, restoreFocus = false) => {
      dropdown.open = false;
      syncCompactDropdown(dropdown);
      if (restoreFocus) dropdown.querySelector("summary")?.focus?.();
    };
    const chooseCompactDropdownValue = (dropdown, value) => {
      const select = dropdown.querySelector("select");
      if (select && select.value !== value) {
        select.value = value;
        select.dispatchEvent(new Event("change", { bubbles: true }));
      }
      setCompactDropdownValue(dropdown, value);
      closeCompactDropdown(dropdown, true);
      compactDropdownHandlers.get(dropdown)?.(dropdown.dataset.publicUiDropdownValue);
    };
    compactDropdowns.forEach(dropdown => {
      const trigger = dropdown.querySelector("summary");
      const options = [...dropdown.querySelectorAll("[data-public-ui-dropdown-option]")];
      if (!trigger || options.length === 0) return;
      const select = dropdown.querySelector("select");
      const initial = select?.value ?? options.find(option => option.getAttribute("aria-selected") === "true")?.dataset.publicUiDropdownOption;
      setCompactDropdownValue(dropdown, initial);
      if (select) {
        const summary = dropdown.querySelector("summary");
        select.addEventListener("change", () => {
          setCompactDropdownValue(dropdown, select.value);
          if (select.checkValidity()) summary?.removeAttribute("aria-invalid");
        });
        select.addEventListener("invalid", () => {
          summary?.setAttribute("aria-invalid", "true");
          summary?.focus({ preventScroll: true });
        });
      }
      syncCompactDropdown(dropdown);
      dropdown.addEventListener("toggle", () => syncCompactDropdown(dropdown));
      const focusableOptions = () => options.filter(option => !option.disabled);
      const moveFocus = (index, event) => {
        event.preventDefault();
        dropdown.open = true;
        syncCompactDropdown(dropdown);
        const available = focusableOptions();
        available[Math.max(0, Math.min(available.length - 1, index))]?.focus?.();
      };
      trigger.addEventListener("keydown", event => {
        const selectedIndex = focusableOptions().findIndex(option => option.dataset.publicUiDropdownOption === dropdown.dataset.publicUiDropdownValue);
        if (event.key === "ArrowDown") moveFocus(selectedIndex + 1, event);
        else if (event.key === "ArrowUp") moveFocus(selectedIndex - 1, event);
        else if (event.key === "Home") moveFocus(0, event);
        else if (event.key === "End") moveFocus(options.length - 1, event);
        else if (event.key === "Escape" && dropdown.open) {
          event.preventDefault();
          closeCompactDropdown(dropdown, true);
        }
      });
      options.forEach(option => {
        if (option.disabled) return;
        option.addEventListener("click", event => {
          event.preventDefault();
          chooseCompactDropdownValue(dropdown, option.dataset.publicUiDropdownOption);
        });
        option.addEventListener("keydown", event => {
          const currentIndex = focusableOptions().indexOf(event.target);
          if (event.key === "ArrowDown") moveFocus(currentIndex + 1, event);
          else if (event.key === "ArrowUp") moveFocus(currentIndex - 1, event);
          else if (event.key === "Home") moveFocus(0, event);
          else if (event.key === "End") moveFocus(options.length - 1, event);
          else if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            chooseCompactDropdownValue(dropdown, option.dataset.publicUiDropdownOption);
          }
          else if (event.key === "Escape") {
            event.preventDefault();
            closeCompactDropdown(dropdown, true);
          }
        });
      });
    });
    root.addEventListener?.("click", event => {
      compactDropdowns.forEach(dropdown => {
        if (dropdown.open && !dropdown.contains?.(event.target)) closeCompactDropdown(dropdown);
      });
    });

    const mastheadMetricSelector = root.querySelector("[data-public-masthead-metric-selector]");
    if (mastheadMetricSelector) {
      const mastheadMetrics = [...root.querySelectorAll("[data-public-masthead-metric]")];
      const metricKeys = new Set(["spooned", "drops", "ehb"]);
      const storageKey = `public-leaderboard-masthead-metric:${mastheadMetricSelector.dataset.publicMastheadEvent || "default"}`;
      let storedMetric = null;
      try { storedMetric = windowObject.sessionStorage?.getItem(storageKey) ?? null; } catch { /* session storage is best effort */ }
      const setMastheadMetric = metric => {
        const active = metricKeys.has(metric) ? metric : "spooned";
        setCompactDropdownValue(mastheadMetricSelector, active);
        mastheadMetrics.forEach(panel => { panel.hidden = panel.dataset.publicMastheadMetric !== active; });
      };
      compactDropdownHandlers.set(mastheadMetricSelector, metric => {
        setMastheadMetric(metric);
        try { windowObject.sessionStorage?.setItem(storageKey, metric); } catch { /* session storage is best effort */ }
      });
      setMastheadMetric(storedMetric);
    }

    const leaderboardsLayout = root.querySelector("[data-public-leaderboards-layout]");
    const railToggle = root.querySelector("[data-public-leaderboards-rail-toggle]");
    const railBody = root.querySelector("[data-public-leaderboards-rail-body]");
    if (leaderboardsLayout && railToggle && railBody) {
      const stackedMedia = windowObject.matchMedia?.("(max-width: 900px)");
      const syncRail = () => {
        const stacked = Boolean(stackedMedia?.matches);
        const collapsed = !stacked && leaderboardsLayout.classList.contains("is-rail-collapsed");
        railToggle.disabled = stacked;
        railToggle.setAttribute("aria-expanded", String(stacked || !collapsed));
        railToggle.setAttribute("aria-label", collapsed
          ? railToggle.dataset.collapsedLabel
          : railToggle.dataset.expandedLabel);
        if (stacked) railToggle.setAttribute("aria-disabled", "true");
        else railToggle.removeAttribute("aria-disabled");
      };
      railToggle.addEventListener("click", event => {
        if (stackedMedia?.matches) return;
        event.preventDefault();
        leaderboardsLayout.classList.toggle("is-rail-collapsed");
        syncRail();
      });
      if (stackedMedia?.addEventListener) stackedMedia.addEventListener("change", syncRail);
      else stackedMedia?.addListener?.(syncRail);
      syncRail();
    }

    const switcher = root.querySelector("[data-public-leaderboard-switcher]");
    if (!switcher) return;

    const links = [...switcher.querySelectorAll("[data-public-leaderboard-view]")];
    const panels = [...root.querySelectorAll("[data-public-leaderboard-panel]")];
    const standingsMetrics = [...root.querySelectorAll("[data-public-leaderboard-standings-metric]")];
    const setRanking = ranking => {
      const active = ranking === "drops" ? "drops" : ranking === "players" ? "players" : "activity";
      links.forEach(link => {
        const current = link.dataset.publicLeaderboardView === active;
        link.classList.toggle("is-current", current);
        if (current) link.setAttribute("aria-current", "page");
        else link.removeAttribute("aria-current");
      });
      panels.forEach(panel => { panel.hidden = panel.dataset.publicLeaderboardPanel !== active; });
      const standingsMetric = active === "drops" ? "drops" : "activity";
      standingsMetrics.forEach(metric => { metric.hidden = metric.dataset.publicLeaderboardStandingsMetric !== standingsMetric; });
    };
    const rankingFromUrl = () => new URL(windowObject.location.href).searchParams.get("ranking");

    setRanking(rankingFromUrl());
    switcher.addEventListener("click", event => {
      const link = event.target.closest?.("[data-public-leaderboard-view]");
      if (!link || event.defaultPrevented || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

      event.preventDefault();
      const target = new URL(link.href || link.getAttribute("href"), windowObject.location.href);
      target.searchParams.set("view", "leaderboards");
      target.searchParams.set("ranking", link.dataset.publicLeaderboardView);
      setRanking(link.dataset.publicLeaderboardView);
      if (target.href !== windowObject.location.href) windowObject.history?.pushState?.({}, "", target.href);
    });
    windowObject.addEventListener?.("popstate", () => setRanking(rankingFromUrl()));
  }

  const api = { initialize };
  if (typeof window !== "undefined") {
    window.publicLeaderboards = api;
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", () => initialize());
    else initialize();
  }
  if (typeof module !== "undefined" && module.exports) module.exports = api;
})();

(function () {
  const initializedRoots = new WeakMap();
  const windowStates = new WeakMap();
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
    menu.id = "public-ui-select-dropdown-" + (++selectDropdownId);
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

  function syncCompactDropdown(dropdown) {
    const trigger = dropdown.querySelector("summary");
    dropdown.dataset.publicUiDropdownOpen = String(dropdown.open);
    if (trigger) trigger.setAttribute("aria-expanded", String(dropdown.open));
    const menu = dropdown.querySelector(".public-ui-compact-dropdown-menu");
    if (menu) {
      if (dropdown.open) menu.removeAttribute("hidden");
      else menu.setAttribute("hidden", "");
    }
  }

  function setCompactDropdownValue(dropdown, value) {
    const options = [...dropdown.querySelectorAll("[data-public-ui-dropdown-option]")];
    const option = options.find(candidate => candidate.dataset.publicUiDropdownOption === value) ?? options[0];
    if (!option) return;

    const active = option.dataset.publicUiDropdownOption;
    dropdown.dataset.publicUiDropdownValue = active;
    const label = dropdown.querySelector("[data-public-ui-dropdown-label]");
    if (label) {
      const optionLabel = option.dataset.publicUiDropdownLabel || active;
      const metricPrefix = dropdown.dataset.publicLeaderboardMetricSelector !== undefined ? "METRIC: " : "";
      label.textContent = metricPrefix && !optionLabel.startsWith(metricPrefix) ? metricPrefix + optionLabel : optionLabel;
    }
    options.forEach(candidate => {
      const selected = candidate === option;
      candidate.setAttribute("aria-selected", String(selected));
      const check = candidate.querySelector("[data-public-ui-dropdown-check]");
      if (check) {
        if (selected) check.removeAttribute("hidden");
        else check.setAttribute("hidden", "");
      }
    });
  }

  function closeCompactDropdown(dropdown, restoreFocus = false) {
    dropdown.open = false;
    syncCompactDropdown(dropdown);
    if (restoreFocus) dropdown.querySelector("summary")?.focus?.();
  }

  function chooseCompactDropdownValue(dropdown, value, handlers) {
    const select = dropdown.querySelector("select");
    if (select && select.value !== value) {
      select.value = value;
      select.dispatchEvent(new Event("change", { bubbles: true }));
    }
    setCompactDropdownValue(dropdown, value);
    closeCompactDropdown(dropdown, true);
    handlers.get(dropdown)?.(value);
  }

  function createWindowState(root, windowObject) {
    const existing = windowStates.get(windowObject);
    if (existing) return existing;

    const ownerDocument = root?.nodeType === 9 ? root : root?.ownerDocument;
    const state = { dropdowns: new Set(), controller: null, ownerDocument, hasDocumentListeners: false };
    windowStates.set(windowObject, state);

    if (ownerDocument?.addEventListener) {
      state.hasDocumentListeners = true;
      ownerDocument.addEventListener("click", event => {
        state.dropdowns.forEach(dropdown => {
          if (dropdown.open && !dropdown.contains?.(event.target)) closeCompactDropdown(dropdown);
        });
      });
      ownerDocument.addEventListener("keydown", event => {
        if (event.key !== "Escape") return;
        state.dropdowns.forEach(dropdown => {
          if (dropdown.open) closeCompactDropdown(dropdown, true);
        });
      });
    }
    windowObject.addEventListener?.("popstate", () => state.controller?.handleNavigation?.(windowObject.location?.href));
    return state;
  }

  function setupCompactDropdowns(root, windowObject, windowState) {
    const handlers = new Map();
    const compactDropdowns = [...root.querySelectorAll("[data-public-ui-compact-dropdown]")];
    compactDropdowns.forEach(dropdown => windowState.dropdowns.add(dropdown));

    compactDropdowns.forEach(dropdown => {
      const trigger = dropdown.querySelector("summary");
      const options = [...dropdown.querySelectorAll("[data-public-ui-dropdown-option]")];
      if (!trigger || options.length === 0) return;
      const summary = trigger;

      const select = dropdown.querySelector("select");
      const initial = select?.value ?? options.find(option => option.getAttribute("aria-selected") === "true")?.dataset.publicUiDropdownOption;
      setCompactDropdownValue(dropdown, initial);
      if (select) {
        select.addEventListener("change", () => {
          setCompactDropdownValue(dropdown, select.value);
          if (select.checkValidity()) summary?.removeAttribute("aria-invalid");
        });
        select.addEventListener("invalid", () => {
          trigger.setAttribute("aria-invalid", "true");
          summary?.focus({ preventScroll: true });
        });
      }

      syncCompactDropdown(dropdown);
      dropdown.addEventListener("toggle", () => syncCompactDropdown(dropdown));
      trigger.addEventListener("click", event => {
        event.preventDefault();
        dropdown.open = !dropdown.open;
        syncCompactDropdown(dropdown);
      });

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
        const choose = () => {
          chooseCompactDropdownValue(dropdown, option.dataset.publicUiDropdownOption, handlers);
        };
        option.addEventListener("click", event => {
          event.preventDefault();
          choose();
        });
        option.addEventListener("keydown", event => {
          const currentIndex = focusableOptions().indexOf(event.target);
          if (event.key === "ArrowDown") moveFocus(currentIndex + 1, event);
          else if (event.key === "ArrowUp") moveFocus(currentIndex - 1, event);
          else if (event.key === "Home") moveFocus(0, event);
          else if (event.key === "End") moveFocus(options.length - 1, event);
          else if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            choose();
          } else if (event.key === "Escape") {
            event.preventDefault();
            closeCompactDropdown(dropdown, true);
          }
        });
      });
    });

    if (!windowState.hasDocumentListeners) {
      root.addEventListener?.("click", event => {
        compactDropdowns.forEach(dropdown => {
          if (dropdown.open && !dropdown.contains?.(event.target)) closeCompactDropdown(dropdown);
        });
      });
    }
    return { handlers };
  }

  function setupDisclosures(root) {
    const details = [...root.querySelectorAll("[data-public-leaderboard-details]")];
    const controls = [...root.querySelectorAll("[data-public-leaderboard-expand-control]")];
    const detailsById = new Map(details.map(detail => [detail.id, detail]));
    const validPairs = controls.map(control => [detailsById.get(control.getAttribute("aria-controls")), control]).filter(([detail]) => detail);
    if (validPairs.length === 0) return;

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

  function setupSorting(root) {
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
      let activeKey = table.dataset.publicLeaderboardSortKey || null;
      let activeDirection = table.dataset.publicLeaderboardSortDirection || null;
      const datasetKey = key => "sort" + key.split("-").map(part => part[0].toUpperCase() + part.slice(1)).join("");
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
        table.dataset.publicLeaderboardSortKey = activeKey || "";
        table.dataset.publicLeaderboardSortDirection = activeDirection || "";
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
      const applySort = () => {
        if (!activeKey || !activeDirection) return;
        const activeControl = sortControls.find(control => control.dataset.publicLeaderboardSortKey === activeKey);
        if (!activeControl) return;
        const numeric = activeControl.dataset.publicLeaderboardSortType === "number";
        const property = datasetKey(activeKey);
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
      };

      sortControls.forEach(control => control.addEventListener("click", () => {
        const key = control.dataset.publicLeaderboardSortKey;
        const numeric = control.dataset.publicLeaderboardSortType === "number";
        const defaultDirection = key === "rank" ? "ascending" : numeric ? "descending" : "ascending";
        activeDirection = key === activeKey
          ? activeDirection === "ascending" ? "descending" : "ascending"
          : defaultDirection;
        activeKey = key;
        applySort();
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
      if (activeKey && activeDirection) applySort();
      setSortState();
      renderPage();
    });
  }

  function setupMasthead(root, windowObject, compactDropdowns) {
    const selector = root.querySelector("[data-public-masthead-metric-selector]");
    if (!selector) return;
    const metrics = [...root.querySelectorAll("[data-public-masthead-metric]")];
    const metricKeys = new Set(["spooned", "drops", "ehb"]);
    const storageKey = "public-leaderboard-masthead-metric:" + (selector.dataset.publicMastheadEvent || "default");
    let storedMetric = null;
    try { storedMetric = windowObject.sessionStorage?.getItem(storageKey) ?? null; } catch { /* session storage is best effort */ }
    const setMetric = metric => {
      const active = metricKeys.has(metric) ? metric : "spooned";
      setCompactDropdownValue(selector, active);
      metrics.forEach(panel => { panel.hidden = panel.dataset.publicMastheadMetric !== active; });
    };
    compactDropdowns.handlers.set(selector, metric => {
      setMetric(metric);
      try { windowObject.sessionStorage?.setItem(storageKey, metric); } catch { /* session storage is best effort */ }
    });
    setMetric(storedMetric);
  }

  function findLayout(root) {
    if (root?.dataset?.publicLeaderboardsLayout !== undefined) return root;
    return root?.querySelector?.("[data-public-leaderboards-layout]") ?? null;
  }

  function metricFromHref(href) {
    try { return new URL(href).searchParams.get("metric") || "default"; } catch { return "default"; }
  }

  function rankingFromHref(href) {
    try { return new URL(href).searchParams.get("ranking"); } catch { return null; }
  }

  function captureLeaderboardState(layout, windowObject) {
    return {
      railCollapsed: layout.classList.contains("is-rail-collapsed"),
      openDetails: [...layout.querySelectorAll("[data-public-leaderboard-details]")].filter(detail => detail.open && detail.id).map(detail => detail.id),
      sorts: [...layout.querySelectorAll("[data-public-leaderboard-sort-table]")].map(table => ({
        key: table.dataset.publicLeaderboardSortKey || null,
        direction: table.dataset.publicLeaderboardSortDirection || null
      })),
      scrollX: windowObject.scrollX ?? 0,
      scrollY: windowObject.scrollY ?? 0
    };
  }

  function restoreLeaderboardState(layout, snapshot) {
    layout.classList.toggle("is-rail-collapsed", snapshot.railCollapsed);
    snapshot.openDetails.forEach(id => {
      const detail = layout.querySelector("#" + id);
      if (detail) detail.open = true;
    });
    [...layout.querySelectorAll("[data-public-leaderboard-sort-table]")].forEach((table, index) => {
      const sort = snapshot.sorts[index];
      if (!sort?.key || !sort.direction) return;
      table.dataset.publicLeaderboardSortKey = sort.key;
      table.dataset.publicLeaderboardSortDirection = sort.direction;
    });
  }

  function parseLeaderboardLayout(html, windowObject) {
    if (html?.querySelector) return html.querySelector("[data-public-leaderboards-layout]");
    const Parser = windowObject.DOMParser || globalThis.DOMParser;
    if (typeof Parser !== "function") return null;
    const parsed = new Parser().parseFromString(html, "text/html");
    return parsed?.querySelector?.("[data-public-leaderboards-layout]") ?? null;
  }

  function setupLeaderboardController(layout, windowObject, windowState, compactDropdowns) {
    const selector = layout.querySelector("[data-public-leaderboard-metric-selector]");
    const switcher = layout.querySelector("[data-public-leaderboard-switcher]");
    const links = switcher ? [...switcher.querySelectorAll("[data-public-leaderboard-view]")] : [];
    const panels = [...layout.querySelectorAll("[data-public-leaderboard-panel]")];
    const standingsMetrics = [...layout.querySelectorAll("[data-public-leaderboard-standings-metric]")];
    const railToggle = layout.querySelector("[data-public-leaderboards-rail-toggle]");
    const railBody = layout.querySelector("[data-public-leaderboards-rail-body]");
    const mode = layout.dataset.publicLeaderboardsMode;
    const state = { metricKey: metricFromHref(windowObject.location?.href), requestSequence: 0 };

    const setRanking = ranking => {
      const available = links.map(link => link.dataset.publicLeaderboardView).filter(Boolean);
      const active = available.includes(ranking) ? ranking : available[0] || "activity";
      links.forEach(link => {
        const current = link.dataset.publicLeaderboardView === active;
        link.classList.toggle("is-current", current);
        if (current) link.setAttribute("aria-current", "page");
        else link.removeAttribute("aria-current");
      });
      panels.forEach(panel => {
        const panelMode = panel.dataset.publicLeaderboardMode;
        const panelView = panel.dataset.publicLeaderboardPanelView || panel.dataset.publicLeaderboardPanel;
        panel.hidden = panelView !== active || Boolean(mode && panelMode && panelMode !== mode);
      });
      const standingsMetric = mode === "metric" ? "boss" : active === "drops" ? "drops" : "activity";
      standingsMetrics.forEach(metric => { metric.hidden = metric.dataset.publicLeaderboardStandingsMetric !== standingsMetric; });
    };

    const restoreUrlAfterFailure = previousHref => {
      if (windowObject.history?.replaceState) windowObject.history.replaceState({}, "", previousHref);
      else windowObject.location.href = previousHref;
      state.metricKey = metricFromHref(previousHref);
      if (selector) setCompactDropdownValue(selector, state.metricKey === "default" ? "default" : state.metricKey);
      setRanking(rankingFromHref(previousHref));
      if (selector) selector.removeAttribute("aria-busy");
    };

    const loadUrl = (href, pushHistory) => {
      const target = new URL(href, windowObject.location.href);
      const requestedMetric = metricFromHref(target.href);
      const previousHref = windowObject.location.href;
      if (requestedMetric === state.metricKey) {
        if (pushHistory && target.href !== previousHref) windowObject.history?.pushState?.({}, "", target.href);
        setRanking(rankingFromHref(target.href));
        return Promise.resolve();
      }

      if (pushHistory) windowObject.history?.pushState?.({}, "", target.href);
      const fetchFunction = windowObject.fetch;
      if (typeof fetchFunction !== "function") {
        windowObject.location.href = target.href;
        return Promise.resolve();
      }

      const requestSequence = ++state.requestSequence;
      if (selector) selector.setAttribute("aria-busy", "true");
      return Promise.resolve()
        .then(() => fetchFunction.call(windowObject, target.href, { headers: { Accept: "text/html", "X-Requested-With": "XMLHttpRequest" } }))
        .then(response => {
          if (requestSequence !== state.requestSequence) return null;
          if (response?.ok === false || typeof response?.text !== "function") throw new Error("Leaderboard response unavailable");
          return response.text();
        })
        .then(html => {
          if (html === null || requestSequence !== state.requestSequence) return;
          const nextLayout = parseLeaderboardLayout(html, windowObject);
          if (!nextLayout) throw new Error("Leaderboard layout missing from response");
          const snapshot = captureLeaderboardState(layout, windowObject);
          restoreLeaderboardState(nextLayout, snapshot);
          if (typeof layout.replaceWith === "function") layout.replaceWith(nextLayout);
          else if (layout.parentElement?.replaceChild) layout.parentElement.replaceChild(nextLayout, layout);
          else throw new Error("Leaderboard layout cannot be replaced");
          initialize(nextLayout, windowObject);
          state.metricKey = requestedMetric;
          windowObject.scrollTo?.(snapshot.scrollX, snapshot.scrollY);
        })
        .catch(() => {
          if (requestSequence !== state.requestSequence) return;
          restoreUrlAfterFailure(previousHref);
        });
    };

    const controller = {
      handleNavigation(href) {
        if (href) loadUrl(href, false);
      },
      selectMetric(metric) {
        const target = new URL(windowObject.location.href);
        target.searchParams.set("view", "leaderboards");
        const currentRanking = rankingFromHref(target.href);
        const defaultRanking = selector?.dataset.publicLeaderboardMetricDefaultRanking || "activity";
        if (metric === "default") {
          target.searchParams.delete("metric");
          target.searchParams.set("ranking", ["activity", "drops", "players"].includes(currentRanking) ? currentRanking : defaultRanking);
        } else {
          target.searchParams.set("metric", metric);
          target.searchParams.set("ranking", ["teams", "players"].includes(currentRanking) ? currentRanking : "teams");
        }
        loadUrl(target.href, true);
      }
    };

    if (selector) compactDropdowns.handlers.set(selector, metric => controller.selectMetric(metric));
    if (railToggle && railBody) {
      const stackedMedia = windowObject.matchMedia?.("(max-width: 900px)");
      const syncRail = () => {
        const stacked = Boolean(stackedMedia?.matches);
        const collapsed = !stacked && layout.classList.contains("is-rail-collapsed");
        railBody.hidden = collapsed;
        railToggle.disabled = stacked;
        railToggle.setAttribute("aria-expanded", String(stacked || !collapsed));
        railToggle.setAttribute("aria-label", collapsed ? railToggle.dataset.collapsedLabel : railToggle.dataset.expandedLabel);
        if (stacked) railToggle.setAttribute("aria-disabled", "true");
        else railToggle.removeAttribute("aria-disabled");
      };
      railToggle.addEventListener("click", event => {
        if (stackedMedia?.matches) return;
        event.preventDefault();
        layout.classList.toggle("is-rail-collapsed");
        syncRail();
      });
      if (stackedMedia?.addEventListener) stackedMedia.addEventListener("change", syncRail);
      else stackedMedia?.addListener?.(syncRail);
      syncRail();
    }
    if (switcher) {
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
    }
    setRanking(rankingFromHref(windowObject.location.href));
    windowState.controller = controller;
    return controller;
  }

  function initialize(root = document, windowObject = window) {
    if (!root || !windowObject) return;
    if (initializedRoots.has(root)) return initializedRoots.get(root);

    const windowState = createWindowState(root, windowObject);
    enhanceSelectDropdowns(root);
    const compactDropdowns = setupCompactDropdowns(root, windowObject, windowState);
    setupDisclosures(root);
    setupSorting(root);
    setupMasthead(root, windowObject, compactDropdowns);
    const layout = findLayout(root);
    const controller = layout ? setupLeaderboardController(layout, windowObject, windowState, compactDropdowns) : null;
    const state = { root, layout, controller };
    initializedRoots.set(root, state);
    return state;
  }

  const api = { initialize };
  if (typeof window !== "undefined") {
    window.publicLeaderboards = api;
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", () => initialize());
    else initialize();
  }
  if (typeof module !== "undefined" && module.exports) module.exports = api;
})();

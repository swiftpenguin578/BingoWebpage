// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
initializeInputModality();

window.bingoDateTimePickerOptions = (overrides = {}) => ({
  enableTime: true,
  enableSeconds: false,
  time_24hr: true,
  minuteIncrement: 5,
  disableMobile: true,
  allowInput: false,
  ...overrides
});

window.bingoDateTimeHours = () => Array.from({ length: 24 }, (_, hour) => String(hour).padStart(2, "0"));
window.bingoDateTimeMinutes = () => Array.from({ length: 12 }, (_, index) => String(index * 5).padStart(2, "0"));

window.initializeBingoDateTimePicker = (input, overrides = {}) => {
  if (typeof window.flatpickr !== "function" || input._flatpickr) return input._flatpickr;
  const { onChange: changed, onReady: ready, timeLabel = document.documentElement?.dataset.timeLabel || document.body?.dataset.timeLabel || "", ...options } = overrides;
  let hourSelect;
  let minuteSelect;
  const sync = (dates) => {
    if (!hourSelect || !minuteSelect || !dates?.length) return;
    const value = dates[0];
    hourSelect.value = String(value.getHours()).padStart(2, "0");
    minuteSelect.value = String(value.getMinutes()).padStart(2, "0");
  };
  return window.flatpickr(input, window.bingoDateTimePickerOptions({
    ...options,
    onReady: (dates, value, instance) => {
      const timeContainer = instance.timeContainer;
      if (timeContainer) {
        instance.calendarContainer.classList.add("event-calendar-picker");
        timeContainer.classList.add("event-calendar-time");
        const label = document.createElement("span");
        label.className = "event-calendar-time-label";
        label.textContent = timeLabel;
        hourSelect = document.createElement("select");
        hourSelect.className = "event-time-select";
        hourSelect.setAttribute("aria-label", `${timeLabel} ${document.documentElement?.dataset.timeHour || document.body?.dataset.timeHour || ""}`.trim());
        window.bingoDateTimeHours().forEach((hour) => hourSelect.add(new Option(hour, hour)));
        minuteSelect = document.createElement("select");
        minuteSelect.className = "event-time-select";
        minuteSelect.setAttribute("aria-label", `${timeLabel} ${document.documentElement?.dataset.timeMinute || document.body?.dataset.timeMinute || ""}`.trim());
        window.bingoDateTimeMinutes().forEach((minute) => minuteSelect.add(new Option(minute, minute)));
        const setTime = () => {
          const selected = instance.selectedDates[0] ? new Date(instance.selectedDates[0]) : new Date();
          selected.setHours(Number(hourSelect.value), Number(minuteSelect.value), 0, 0);
          instance.setDate(selected, true);
        };
        hourSelect.addEventListener("change", setTime);
        minuteSelect.addEventListener("change", setTime);
        label.append(hourSelect, minuteSelect);
        timeContainer.append(label);
      }
      sync(dates);
      ready?.(dates, value, instance);
    },
    onChange: (dates, value, instance) => {
      sync(dates);
      changed?.(dates, value, instance);
    }
  }));
};

function initializePublicHeaderPopovers() {
  const menus = [...document.querySelectorAll(".nav-popover details, [data-public-ui-popover]")];
  if (!menus.length) return;

  const panelFor = menu => menu.querySelector("[data-public-ui-popover-panel]");
  const triggerFor = menu => menu.querySelector("button[aria-controls]");
  const isPublicMenu = menu => menu.matches("[data-public-ui-popover]");
  const isOpen = menu => isPublicMenu(menu)
    ? triggerFor(menu)?.getAttribute("aria-expanded") === "true"
    : menu.open;
  const syncPanel = menu => {
    const panel = panelFor(menu);
    const trigger = triggerFor(menu);
    if (!isPublicMenu(menu) || !panel || !trigger) return;
    panel.hidden = trigger.getAttribute("aria-expanded") !== "true";
  };
  const setOpen = (menu, open, returnFocus = false) => {
    const panel = panelFor(menu);
    const trigger = triggerFor(menu);
    if (isPublicMenu(menu) && panel && trigger) {
      panel.hidden = !open;
      trigger.setAttribute("aria-expanded", String(open));
      if (returnFocus) trigger.focus();
      return;
    }
    menu.open = open;
    if (!open && returnFocus) menu.querySelector("summary")?.focus();
  };
  const closeMenu = menu => setOpen(menu, false);

  for (const menu of menus) {
    syncPanel(menu);
    if (menu.dataset.publicUiPopoverReady === "true") continue;
    menu.dataset.publicUiPopoverReady = "true";
    if (isPublicMenu(menu)) {
      const trigger = triggerFor(menu);
      trigger?.addEventListener("click", () => {
        const open = !isOpen(menu);
        if (open) {
          for (const other of document.querySelectorAll(".nav-popover details, [data-public-ui-popover]")) {
            if (other !== menu) closeMenu(other);
          }
        }
        setOpen(menu, open);
      });
    }
    if (isPublicMenu(menu) || menu.closest("[data-admin-event-selector]")) {
      menu.addEventListener("keydown", event => {
        if (event.key !== "Escape") return;
        event.preventDefault();
        setOpen(menu, false, true);
      });
    }
  }

  if (document.documentElement.dataset.publicUiPopoverManagerReady === "true") return;
  document.documentElement.dataset.publicUiPopoverManagerReady = "true";
  document.addEventListener("click", event => {
    const dismissButton = event.target?.closest?.("[data-dismiss-toast]");
    if (dismissButton) dismissTransientToast(dismissButton.closest("[data-transient-toast]"));

    for (const menu of document.querySelectorAll(".nav-popover details, [data-public-ui-popover]")) {
      if (isOpen(menu) && !menu.contains(event.target)) closeMenu(menu);
    }
  });
}

function initializePublicTheme() {
  const control = document.querySelector("[data-public-theme-control]");
  if (!(control instanceof HTMLSelectElement)) return;
  const apply = theme => {
    const nextTheme = theme === "dark" ? "dark" : "light";
    document.documentElement.dataset.publicTheme = nextTheme;
    control.value = nextTheme;
    try { localStorage.setItem("bingo:public-theme", nextTheme); } catch { }
  };
  apply(document.documentElement.dataset.publicTheme);
  control.addEventListener("change", () => apply(control.value));
}

document.addEventListener("DOMContentLoaded", () => {
  initializePublicTheme();
  restorePostNavigationState();
  const pendingToast = sessionStorage.getItem("bingo:pending-toast");
  if (pendingToast) {
    sessionStorage.removeItem("bingo:pending-toast");
    try {
      const toast = JSON.parse(pendingToast);
      window.setTimeout(() => window.showBingoToast?.(toast.message, toast.type), 0);
    } catch {
      // Ignore stale toast state.
    }
  }
  initializePostNavigation();
  initializeCorrectionDropSelectors();
  initializeAutoHideScrollbars();
  initializeAdminMenu();
  initializeAdminEventSection();
  initializeAdminEventDirectorySearch();
  initializeAdminAccountSearch();
  initializeTransientToastLayer();
  const feedback = document.querySelector(".validation-summary-errors");
  if (feedback) {
    feedback.scrollIntoView({ block: "nearest" });
    feedback.focus({ preventScroll: true });
  }

  if (typeof window.flatpickr === "function") {
    document.querySelectorAll("[data-date-time-picker]").forEach((input) => {
      window.initializeBingoDateTimePicker(input, {
        dateFormat: "Y-m-d\\TH:i",
        altInput: true,
        altFormat: "d/m/Y H:i",
        defaultDate: input.value || null,
        minDate: input.dataset.minDate || null,
        maxDate: input.dataset.maxDate || null,
        timeLabel: input.dataset.timeLabel || document.documentElement?.dataset.timeLabel || document.body?.dataset.timeLabel || ""
      });
    });
  }

  initializePublicHeaderPopovers();
});

const transientToastTypes = new Set(["success", "warning", "error", "information"]);
var transientToastIcons = {
  success: '<circle cx="12" cy="12" r="9" /><path d="m8 12 2.5 2.5L16 9" />',
  warning: '<path d="m12 3 9 17H3L12 3Z" /><path d="M12 9v4" /><path d="M12 16h.01" />',
  error: '<circle cx="12" cy="12" r="9" /><path d="m9 9 6 6m0-6-6 6" />',
  information: '<circle cx="12" cy="12" r="9" /><path d="M12 10v6" /><path d="M12 7h.01" />'
};

function normalizeTransientToastType(type) {
  const normalized = String(type || "information").toLowerCase();
  return normalized === "info" ? "information" : transientToastTypes.has(normalized) ? normalized : "information";
}

function dismissTransientToast(toast) {
  if (!(toast instanceof HTMLElement)) return;
  if (toast.dataset.toastDismissing === "true") return;
  if (toast._toastTimer) {
    window.clearTimeout(toast._toastTimer);
    toast._toastTimer = null;
  }
  toast.dataset.toastDismissing = "true";
  if (window.matchMedia?.("(prefers-reduced-motion: reduce)").matches) {
    toast.remove();
    return;
  }
  toast.classList.add("is-dismissing");
  window.setTimeout(() => toast.remove(), 140);
}

function initializeTransientToast(toast) {
  if (!(toast instanceof HTMLElement) || toast.dataset.toastReady === "true") return;
  toast.dataset.toastReady = "true";
  let hovered = false;
  let focused = false;
  let remaining = Number(toast.dataset.toastDuration || 6000);
  let startedAt = 0;

  const pause = () => {
    if (!toast._toastTimer) return;
    remaining -= Math.max(0, Date.now() - startedAt);
    window.clearTimeout(toast._toastTimer);
    toast._toastTimer = null;
  };
  const resume = () => {
    if (hovered || focused || toast._toastTimer || !toast.isConnected) return;
    if (remaining <= 0) { dismissTransientToast(toast); return; }
    startedAt = Date.now();
    toast._toastTimer = window.setTimeout(() => dismissTransientToast(toast), remaining);
  };

  toast.addEventListener("mouseenter", () => { hovered = true; pause(); });
  toast.addEventListener("mouseleave", () => { hovered = false; resume(); });
  toast.addEventListener("focusin", () => { focused = true; pause(); });
  toast.addEventListener("focusout", event => {
    focused = event.relatedTarget instanceof Node && toast.contains(event.relatedTarget);
    if (!focused) resume();
  });
  toast.addEventListener("keydown", event => {
    if (event.key !== "Escape") return;
    event.preventDefault();
    dismissTransientToast(toast);
  });
  toast.querySelector("[data-dismiss-toast]")?.addEventListener("click", () => dismissTransientToast(toast));
  resume();
}

function initializeTransientToastLayer() {
  document.querySelectorAll("[data-transient-toast]").forEach(initializeTransientToast);
  const host = document.querySelector("#app-notice-region");
  if (!host || host.dataset.toastHostObserverReady === "true") return;
  host.dataset.toastHostObserverReady = "true";
  let homeParent = host.parentElement;
  let homeNextSibling = host.nextSibling;
  const syncHostLayer = () => {
    const openDialog = document.querySelector("dialog.admin-route-dialog[open]");
    if (openDialog instanceof HTMLDialogElement && !openDialog.contains(host)) {
      if (!homeParent) { homeParent = host.parentElement; homeNextSibling = host.nextSibling; }
      openDialog.append(host);
      return;
    }
    if (!openDialog && homeParent && !homeParent.contains(host)) {
      homeParent.insertBefore(host, homeNextSibling && homeNextSibling.parentNode === homeParent ? homeNextSibling : null);
    }
  };
  syncHostLayer();
  if (typeof MutationObserver === "function") {
    const observer = new MutationObserver(syncHostLayer);
    observer.observe(document.body, { attributes: true, attributeFilter: ["open"], childList: true, subtree: true });
  }
}

window.initializeTransientToastLayer = initializeTransientToastLayer;

function createTransientToast(message, type) {
  const normalizedType = normalizeTransientToastType(type);
  const labels = document.documentElement?.dataset ?? {};
  const transientToastLabels = {
    success: labels.publicToastSuccess || "",
    warning: labels.publicToastWarning || "",
    error: labels.publicToastError || "",
    information: labels.publicToastInformation || ""
  };
  const dismissLabel = labels.publicToastDismiss || "";
  const toast = document.createElement("div");
  toast.className = `app-toast app-toast-${normalizedType}`;
  toast.setAttribute("role", normalizedType === "error" ? "alert" : "status");
  toast.dataset.transientToast = "true";
  toast.dataset.toastDuration = "6000";
  toast.innerHTML = `<svg class="app-toast-icon" aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8">${transientToastIcons[normalizedType]}</svg><div class="app-toast-copy"><strong>${transientToastLabels[normalizedType]}</strong><span></span></div><button type="button" class="app-toast-dismiss admin-route-dialog-close" data-dismiss-toast aria-label="${dismissLabel}"><svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8"><path d="m6 6 12 12M18 6 6 18" /></svg></button>`;
  toast.querySelector(".app-toast-copy span").textContent = message;
  return toast;
}

document.addEventListener("bingo:content-updated", initializeTransientToastLayer);
document.addEventListener("bingo:content-updated", initializePublicHeaderPopovers);

if (document.readyState !== "loading") initializePublicHeaderPopovers();

function initializeAdminMenu() {
  const toggle = document.querySelector("[data-admin-menu-toggle]");
  const sidebar = toggle instanceof HTMLButtonElement ? document.getElementById(toggle.getAttribute("aria-controls")) : null;
  const scrim = document.querySelector("[data-admin-menu-scrim]");
  if (!(toggle instanceof HTMLButtonElement) || !(sidebar instanceof HTMLElement) || !(scrim instanceof HTMLButtonElement)) return;

  document.documentElement.classList.add("admin-menu-enhanced");
  let open = false;
  let previouslyFocused = null;

  const focusableSelector = [
    "a[href]",
    "button:not([disabled])",
    "input:not([disabled])",
    "select:not([disabled])",
    "textarea:not([disabled])",
    "[tabindex]:not([tabindex=\"-1\"])"
  ].join(",");

  const focusableItems = () => [...sidebar.querySelectorAll(focusableSelector)]
    .filter(element => element instanceof HTMLElement && element.offsetParent !== null);

  const setOpen = (next, restoreFocus = false) => {
    open = next;
    sidebar.classList.toggle("is-open", open);
    sidebar.setAttribute("aria-hidden", String(!open && window.innerWidth <= 900));
    toggle.setAttribute("aria-expanded", String(open));
    scrim.hidden = !open;
    document.body.classList.toggle("admin-menu-open", open);
    if (open) {
      previouslyFocused = document.activeElement;
      focusableItems()[0]?.focus({ preventScroll: true });
    } else if (restoreFocus) {
      (previouslyFocused instanceof HTMLElement ? previouslyFocused : toggle).focus({ preventScroll: true });
    }
    if (!open) previouslyFocused = null;
  };

  const syncDesktopState = () => {
    if (window.innerWidth > 900) {
      open = false;
      sidebar.classList.remove("is-open");
      sidebar.setAttribute("aria-hidden", "false");
      toggle.setAttribute("aria-expanded", "false");
      scrim.hidden = true;
      document.body.classList.remove("admin-menu-open");
    } else {
      sidebar.setAttribute("aria-hidden", String(!open));
    }
  };

  toggle.addEventListener("click", () => setOpen(!open));
  scrim.addEventListener("click", () => setOpen(false, true));
  sidebar.addEventListener("click", event => {
    if (event.target.closest("a") && window.innerWidth <= 900) setOpen(false);
  });
  document.addEventListener("keydown", event => {
    if (!open) return;
    if (event.key === "Tab") {
      const items = focusableItems();
      if (!items.length) return;
      const first = items[0];
      const last = items[items.length - 1];
      if (event.shiftKey && (!sidebar.contains(document.activeElement) || document.activeElement === first)) {
        event.preventDefault();
        last.focus({ preventScroll: true });
      } else if (!event.shiftKey && (!sidebar.contains(document.activeElement) || document.activeElement === last)) {
        event.preventDefault();
        first.focus({ preventScroll: true });
      }
      return;
    }
    if (event.key === "Escape") {
      event.preventDefault();
      setOpen(false, true);
    }
  });
  window.addEventListener("resize", syncDesktopState);
  syncDesktopState();
}

function initializeAdminEventSection() {
  const navigation = document.querySelector("[data-admin-event-navigation]");
  if (!(navigation instanceof HTMLElement)) return;
  const serverActive = navigation.querySelector(".admin-nav-link.is-active");

  const sync = () => {
    navigation.querySelectorAll("[data-admin-event-section]").forEach(link => {
      link.classList.remove("is-active");
      link.removeAttribute("aria-current");
    });

    const active = window.location.hash === "#players"
      ? navigation.querySelector('[data-admin-event-section="participants"]')
      : serverActive;
    if (!(active instanceof HTMLElement)) return;
    active.classList.add("is-active");
    active.setAttribute("aria-current", "page");
  };

  window.addEventListener("hashchange", sync);
  sync();
}

function initializeAdminEventDirectorySearch() {
  const form = document.querySelector("[data-admin-event-directory-search]");
  const page = form?.closest(".admin-events-page");
  const input = form?.querySelector("[data-admin-event-search-input]");
  const clear = form?.querySelector("[data-admin-search-clear]");
  const state = form?.querySelector("[data-admin-event-state-filter]");
  const table = page?.querySelector("[data-admin-event-table]");
  const empty = page?.querySelector("[data-admin-event-filter-empty]");
  const body = table?.tBodies[0];
  const sortLinks = page ? [...page.querySelectorAll("[data-admin-event-sort-link]")] : [];
  if (!(form instanceof HTMLFormElement) || !(input instanceof HTMLInputElement) || !(state instanceof HTMLSelectElement)) return;

  const compareText = (left, right) => left === right ? 0 : left < right ? -1 : 1;
  const compareNumber = (left, right) => Number(left) - Number(right);
  const compareTicks = (left, right) => {
    if (!left && !right) return 0;
    if (!left) return 1;
    if (!right) return -1;
    const leftTicks = BigInt(left);
    const rightTicks = BigInt(right);
    return leftTicks === rightTicks ? 0 : leftTicks < rightTicks ? -1 : 1;
  };
  const compareOptionalTicks = (left, right, direction) => {
    const result = compareTicks(left, right);
    return direction === "desc" && left && right ? -result : result;
  };
  const compareTies = (left, right) => compareText(left.dataset.eventName.toLowerCase(), right.dataset.eventName.toLowerCase())
    || compareText(left.dataset.eventId.toLowerCase(), right.dataset.eventId.toLowerCase());
  const compareRows = (left, right, key, direction) => {
    const descending = direction === "desc";
    let result = 0;
    switch (key) {
      case "identity":
        result = compareText(left.dataset.eventName.toLowerCase(), right.dataset.eventName.toLowerCase())
          || compareText(left.dataset.eventSlug.toLowerCase(), right.dataset.eventSlug.toLowerCase());
        break;
      case "state":
        result = compareNumber(left.dataset.eventStateOrder, right.dataset.eventStateOrder);
        break;
      case "dates":
        result = compareOptionalTicks(left.dataset.eventStart, right.dataset.eventStart, direction)
          || compareOptionalTicks(left.dataset.eventEnd, right.dataset.eventEnd, direction);
        break;
      case "signups":
        result = compareNumber(left.dataset.eventConfirmed, right.dataset.eventConfirmed)
          || compareNumber(left.dataset.eventWaiting, right.dataset.eventWaiting)
          || compareNumber(left.dataset.eventCap, right.dataset.eventCap);
        break;
      case "attention":
        result = compareNumber(left.dataset.eventAttention, right.dataset.eventAttention);
        break;
    }
    if (descending && key !== "dates") result = -result;
    return result || compareTies(left, right);
  };

  const sortRows = (key, direction) => {
    if (!(body instanceof HTMLTableSectionElement)) return;
    const rows = [...body.querySelectorAll("[data-admin-event-row]")];
    rows.sort((left, right) => compareRows(left, right, key, direction));
    body.append(...rows);
  };

  const createSortIcon = direction => {
    const icon = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    icon.setAttribute("class", "admin-events-sort-icon");
    icon.setAttribute("aria-hidden", "true");
    icon.setAttribute("viewBox", "0 0 24 24");
    icon.setAttribute("fill", "none");
    icon.setAttribute("stroke", "currentColor");
    icon.setAttribute("stroke-linecap", "round");
    icon.setAttribute("stroke-linejoin", "round");
    icon.setAttribute("stroke-width", "2");
    const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
    path.setAttribute("d", direction === "desc" ? "m6 9 6 6 6-6" : "m18 15-6-6-6 6");
    icon.append(path);
    return icon;
  };

  const updateSortState = (key, direction) => {
    sortLinks.forEach(link => {
      const header = link.closest("th");
      const active = link.dataset.sortKey === key;
      if (header) {
        if (active) header.setAttribute("aria-sort", direction === "desc" ? "descending" : "ascending");
        else header.removeAttribute("aria-sort");
      }
      link.querySelectorAll(".admin-events-sort-icon").forEach(icon => icon.remove());
      if (active) link.append(createSortIcon(direction));
      const labels = { ...(document.documentElement?.dataset ?? {}), ...(document.body?.dataset ?? {}) };
      const label = link.dataset.sortLabel || labels.sortColumn || "";
      const nextDirection = active && direction === "asc" ? "descending" : "ascending";
      link.setAttribute("aria-label", active
        ? (labels.adminSortCurrentTemplate || labels.sortCurrentTemplate || "").replace("{0}", label).replace("{1}", direction === "desc" ? labels.sortDirectionDescending : labels.sortDirectionAscending).replace("{2}", nextDirection === "descending" ? labels.sortDirectionDescending : labels.sortDirectionAscending)
        : (labels.adminSortTemplate || labels.sortByTemplate || "").replace("{0}", label).replace("{1}", labels.sortDirectionAscending || ""));
    });
  };

  const updateSortLinks = (url, activeKey, activeDirection) => {
    sortLinks.forEach(link => {
      const linkUrl = new URL(url.href);
      linkUrl.searchParams.set("search", input.value.trim());
      linkUrl.searchParams.set("filter", state.value);
      linkUrl.searchParams.set("sort", link.dataset.sortKey);
      linkUrl.searchParams.set("direction", link.dataset.sortKey === activeKey && activeDirection === "asc" ? "desc" : "asc");
      link.href = linkUrl.toString();
    });
  };

  const updateUrl = () => {
    const nextUrl = new URL(window.location.href);
    const values = { search: input.value.trim(), filter: state.value };
    Object.entries(values).forEach(([key, value]) => value ? nextUrl.searchParams.set(key, value) : nextUrl.searchParams.delete(key));
    window.history.replaceState(null, "", `${nextUrl.pathname}${nextUrl.search}${nextUrl.hash}`);
    if (clear) clear.hidden = !values.search;
  };

  const apply = () => {
    const search = input.value.trim().toLowerCase();
    const selectedState = state.value.toLowerCase();
    let visible = 0;
    if (table instanceof HTMLTableElement && empty instanceof HTMLElement) {
      table.querySelectorAll("[data-admin-event-row]").forEach(row => {
        const matchesSearch = !search || row.dataset.eventName?.toLowerCase().includes(search) || row.dataset.eventSlug?.toLowerCase().includes(search);
        const matchesState = selectedState === "all" || row.dataset.eventState === selectedState;
        row.hidden = !(matchesSearch && matchesState);
        if (!row.hidden) visible++;
      });
      table.hidden = visible === 0;
      empty.hidden = visible > 0;
    }
    updateUrl();
  };

  input.addEventListener("input", apply);
  state.addEventListener("change", apply);
  clear?.addEventListener("click", event => {
    event.preventDefault();
    input.value = "";
    apply();
    input.focus();
  });
  form.addEventListener("submit", event => {
    event.preventDefault();
    apply();
  });

  sortLinks.forEach(link => link.addEventListener("click", event => {
    event.preventDefault();
    const targetUrl = new URL(link.href, window.location.href);
    const key = link.dataset.sortKey;
    const direction = targetUrl.searchParams.get("direction") === "desc" ? "desc" : "asc";
    targetUrl.searchParams.set("search", input.value.trim());
    targetUrl.searchParams.set("filter", state.value);
    targetUrl.searchParams.set("sort", key);
    targetUrl.searchParams.set("direction", direction);
    sortRows(key, direction);
    updateSortState(key, direction);
    window.history.replaceState({}, "", targetUrl);
    updateSortLinks(targetUrl, key, direction);
    apply();
  }));

  const initialUrl = new URL(window.location.href);
  if (initialUrl.searchParams.get("sort")) updateSortLinks(initialUrl, initialUrl.searchParams.get("sort"), initialUrl.searchParams.get("direction") === "desc" ? "desc" : "asc");
  apply();
}

function initializeAdminAccountSearch() {
  const page = document.querySelector(".admin-accounts-page");
  if (!(page instanceof HTMLElement)) return;

  const setupSection = (section) => {
    if (!(section instanceof HTMLElement) || section.dataset.adminAccountInitialized === "true") return;
    const form = section.querySelector("[data-admin-account-filter]");
    const input = form?.querySelector(".admin-search-field-input");
    const clear = form?.querySelector("[data-admin-search-clear]");
    const role = form?.querySelector("[data-admin-account-role]");
    if (!(form instanceof HTMLFormElement) || !(input instanceof HTMLInputElement) || !(clear instanceof HTMLButtonElement)) return;

    section.dataset.adminAccountInitialized = "true";
    const sectionKey = section.dataset.adminAccountSection;
    const pageParameter = sectionKey === "website" ? "WebsitePage" : "EmergencyPage";
    let debounceId;
    let activeController;

    const syncClear = () => { clear.hidden = input.value.length === 0; };
    const targetUrl = () => {
      const target = new URL(form.action || window.location.href, window.location.href);
      target.searchParams.delete(pageParameter);
      for (const [name, value] of new FormData(form)) {
        target.searchParams.delete(name);
        if (typeof value === "string" && value.length > 0) target.searchParams.set(name, value);
      }
      target.hash = "";
      return target;
    };

    const apply = async (focusId = input.id, selection = null) => {
      const target = targetUrl();
      const inputValue = input.value;
      activeController?.abort();
      const controller = new AbortController();
      activeController = controller;
      form.setAttribute("aria-busy", "true");
      try {
        const response = await window.fetch(target, {
          credentials: "same-origin",
          headers: { "X-Requested-With": "XMLHttpRequest" },
          signal: controller.signal
        });
        if (!response.ok) throw new Error("Account directory request failed.");
        const html = await response.text();
        const parsed = new DOMParser().parseFromString(html, "text/html");
        const nextSection = parsed.querySelector(`[data-admin-account-section="${sectionKey}"]`);
        if (!(nextSection instanceof HTMLElement)) throw new Error("Account directory section was not returned.");

        const replacement = document.importNode(nextSection, true);
        section.replaceWith(replacement);
        window.history.replaceState(window.history.state, "", `${target.pathname}${target.search}`);
        document.dispatchEvent(new CustomEvent("bingo:account-directory-updated", { detail: { section: replacement } }));
        setupSection(replacement);

        const nextInput = replacement.querySelector(".admin-search-field-input");
        const nextClear = replacement.querySelector("[data-admin-search-clear]");
        if (nextInput instanceof HTMLInputElement) {
          if (focusId === input.id) nextInput.value = inputValue;
          if (nextClear instanceof HTMLButtonElement) nextClear.hidden = nextInput.value.length === 0;
          if (focusId === input.id) {
            nextInput.focus({ preventScroll: true });
            if (selection && selection.every(value => value !== null)) nextInput.setSelectionRange(selection[0], selection[1]);
          } else {
            const nextFocus = [...replacement.querySelectorAll("[id]")].find(element => element.id === focusId);
            if (nextFocus instanceof HTMLElement) nextFocus.focus({ preventScroll: true });
          }
        }
      } catch (error) {
        if (error?.name !== "AbortError") window.location.assign(target.toString());
      } finally {
        if (activeController === controller) {
          activeController = null;
          form.removeAttribute("aria-busy");
        }
      }
    };

    input.addEventListener("input", () => {
      activeController?.abort();
      syncClear();
      window.clearTimeout(debounceId);
      debounceId = window.setTimeout(() => apply(input.id, [input.selectionStart, input.selectionEnd]), 220);
    });
    clear.addEventListener("click", event => {
      event.preventDefault();
      input.value = "";
      syncClear();
      apply(input.id, [0, 0]);
    });
    role?.addEventListener("change", () => apply(role.id));
    form.addEventListener("submit", event => {
      event.preventDefault();
      window.clearTimeout(debounceId);
      apply(document.activeElement instanceof HTMLElement && section.contains(document.activeElement) ? document.activeElement.id : input.id, [input.selectionStart, input.selectionEnd]);
    });
    if (window.location.hash === `#${input.id}`) {
      input.focus({ preventScroll: true });
      const url = new URL(window.location.href);
      url.hash = "";
      window.history.replaceState(window.history.state, "", `${url.pathname}${url.search}`);
    }
    syncClear();
  };

  page.querySelectorAll("[data-admin-account-section]").forEach(setupSection);
}

document.addEventListener("bingo:content-updated", initializeCorrectionDropSelectors);

function initializeCorrectionDropSelectors() {
  document.querySelectorAll("[data-correction-requirement]").forEach(syncCorrectionDropSelector);
}

window.syncCorrectionDropSelector = function (requirement) {
  const form = requirement.closest("form");
  const tile = form?.querySelector("[data-correction-tile]");
  const drop = form?.querySelector("[data-correction-drop]");
  const catalogue = form?.querySelector("[data-correction-drop-catalogue]");
  if (!(tile instanceof HTMLInputElement) || !(drop instanceof HTMLSelectElement) || !(catalogue instanceof HTMLSelectElement)) return;

  const groups = [...catalogue.querySelectorAll("optgroup")].map(group => ({
    label: group.label,
    options: [...group.querySelectorAll("option")].map(option => option.cloneNode(true))
  }));
  const manual = catalogue.querySelector('option[value=""]')?.cloneNode(true);
  const previous = drop.value;
  drop.replaceChildren();
  if (manual) drop.append(manual);
  for (const group of groups) {
    const options = group.options.filter(option => option.dataset.requirementId === requirement.value);
    if (options.length === 0) continue;
    const element = document.createElement("optgroup");
    element.label = group.label;
    element.append(...options);
    drop.append(element);
  }
  tile.value = requirement.selectedOptions[0]?.dataset.tile ?? tile.value;
  if ([...drop.options].some(option => option.value === previous)) drop.value = previous;
}

function initializeInputModality() {
  const root = document.documentElement;
  root.classList.add("pointer-navigation");
  document.addEventListener("keydown", event => {
    if (event.key !== "Tab") return;
    root.classList.add("keyboard-navigation");
    root.classList.remove("pointer-navigation");
  }, true);
  document.addEventListener("pointerdown", () => {
    root.classList.add("pointer-navigation");
    root.classList.remove("keyboard-navigation");
  }, true);
}

function initializeAutoHideScrollbars() {
  const hideTimers = new WeakMap();

  document.addEventListener("scroll", event => {
    const region = event.target;
    if (!(region instanceof HTMLElement) || !region.matches(".auto-hide-scrollbar, .public-ui-auto-hide-scrollbar")) return;

    region.classList.add("scrollbar-active");
    const previousTimer = hideTimers.get(region);
    if (previousTimer) window.clearTimeout(previousTimer);
    hideTimers.set(region, window.setTimeout(() => {
      region.classList.remove("scrollbar-active");
      hideTimers.delete(region);
    }, 700));
  }, true);
}

function initializePostNavigation() {
  document.addEventListener("submit", async event => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement) || event.defaultPrevented) return;

    const submitter = event.submitter;
    const method = (submitter?.getAttribute("formmethod") || form.method || "get").toLowerCase();
    if (method !== "post"
        || form.dataset.historyMode === "push"
        || form.dataset.nativeSubmit !== undefined
        || (form.target && form.target !== "_self")) {
      return;
    }

    const currentUrl = window.location.href;
    const action = new URL(submitter?.getAttribute("formaction") || form.action || currentUrl, currentUrl);
    if (action.origin !== window.location.origin) return;

    const pageState = capturePostNavigationState();

    event.preventDefault();
    if (form.dataset.historySubmitting === "true") return;
    form.dataset.historySubmitting = "true";
    form.setAttribute("aria-busy", "true");

    const formData = new FormData(form);
    if (submitter?.name) formData.append(submitter.name, submitter.value);

    const buttons = [...form.querySelectorAll("button, input[type='submit']")];
    const buttonStates = buttons.map(button => button.disabled);
    buttons.forEach(button => button.disabled = true);

    try {
      const headers = {
        "X-Requested-With": "XMLHttpRequest",
        "X-Bingo-Enhanced-Post": form.dataset.updateTargets ? "partial" : "true"
      };

      const response = await fetch(action, {
        method: "POST",
        body: formData,
        credentials: "same-origin",
        headers
      });

      const disposition = response.headers.get("Content-Disposition") || "";
      if (disposition.toLowerCase().includes("attachment")) {
        await downloadPostResponse(response, disposition);
        restoreForm();
        return;
      }

      const navigation = response.headers.get("X-Bingo-Post-Navigation");
      if (navigation) {
        navigatePostResponse(pageState, currentUrl, navigation);
        return;
      }

      const contentType = response.headers.get("Content-Type") || "";
      if (contentType.toLowerCase().includes("text/html")) {
        const html = await response.text();
        const destination = response.redirected ? response.url : currentUrl;
        const updatedSelectors = updatePostTargets(form, html, currentUrl, destination);
        if (updatedSelectors) {
          replaceSamePageHistory(currentUrl, destination);
          document.dispatchEvent(new CustomEvent("bingo:content-updated", {
            detail: { selectors: updatedSelectors }
          }));
          applyPostNavigationState(pageState);
          return;
        }

        if (!rememberPostNavigationState(pageState, currentUrl, destination)) {
          window.location.assign(destination);
          return;
        }
        document.open();
        document.write(html);
        document.close();
        return;
      }

      window.location.replace(response.url || action.href);
    } catch {
      restoreForm();
      window.alert(document.documentElement?.dataset.postError || document.body?.dataset.adminPostError || "");
    }

    function restoreForm() {
      delete form.dataset.historySubmitting;
      form.removeAttribute("aria-busy");
      buttons.forEach((button, index) => button.disabled = buttonStates[index]);
    }
  });
}

function navigatePostResponse(pageState, currentUrl, destination) {
  if (rememberPostNavigationState(pageState, currentUrl, destination)) {
    window.location.replace(destination);
    return;
  }

  window.location.assign(destination);
}

function updatePostTargets(form, html, currentUrl, destination) {
  const selectorText = form.dataset.updateTargets;
  if (!selectorText) return null;

  const current = new URL(currentUrl);
  const next = new URL(destination, currentUrl);
  if (current.origin !== next.origin || current.pathname !== next.pathname) return null;

  const selectors = selectorText.split(",").map(selector => selector.trim()).filter(Boolean);
  const responseDocument = new DOMParser().parseFromString(html, "text/html");
  const replacements = selectors.map(selector => ({
    selector,
    current: document.querySelector(selector),
    next: responseDocument.querySelector(selector)
  }));
  if (replacements.some(item => !item.current || !item.next)) return null;

  document.dispatchEvent(new CustomEvent("bingo:content-will-update", {
    detail: { selectors, form }
  }));
  replacements.forEach(item => {
    item.current.querySelectorAll("input").forEach(input => input._flatpickr?.destroy());
    item.current.replaceWith(document.importNode(item.next, true));
  });
  return selectors;
}

var postNavigationStateKey = "bingo:post-navigation-state";

function isSamePostNavigationPage(currentUrl, destination) {
  const current = new URL(currentUrl);
  const next = new URL(destination, currentUrl);
  return current.origin === next.origin && current.pathname === next.pathname;
}

function replaceSamePageHistory(currentUrl, destination) {
  if (!isSamePostNavigationPage(currentUrl, destination)) return false;
  window.history.replaceState(window.history.state, "", destination);
  return true;
}

function capturePostNavigationState() {
  const details = [...document.querySelectorAll("details")]
    .filter(detail => !detail.closest(".nav-popover") && !detail.matches("[data-no-post-restore]"))
    .map((detail, index) => ({ key: postNavigationDetailsKey(detail, index), open: detail.open }));
  const openDialogs = [...document.querySelectorAll("dialog[data-preserve-post-dialog][open][id]")]
    .map(dialog => dialog.id);
  const focused = document.activeElement instanceof HTMLElement ? document.activeElement : null;

  return {
    scrollX: window.scrollX,
    scrollY: window.scrollY,
    details,
    openDialogs,
    focusedSelector: postNavigationFocusSelector(focused)
  };
}

function rememberPostNavigationState(state, currentUrl, destination) {
  const next = new URL(destination, currentUrl);
  if (!isSamePostNavigationPage(currentUrl, destination)) {
    sessionStorage.removeItem(postNavigationStateKey);
    return false;
  }

  sessionStorage.setItem(postNavigationStateKey, JSON.stringify({
    ...state,
    path: next.pathname,
    destination: `${next.pathname}${next.search}${next.hash}`,
    savedAt: Date.now()
  }));
  return true;
}

function restorePostNavigationState() {
  const serialized = sessionStorage.getItem(postNavigationStateKey);
  if (!serialized) return;
  sessionStorage.removeItem(postNavigationStateKey);

  try {
    const state = JSON.parse(serialized);
    if (state.path !== window.location.pathname || Date.now() - state.savedAt > 30_000) return;
    replaceSamePageHistory(window.location.href, state.destination || window.location.href);
    applyPostNavigationState(state);
  } catch {
    // A stale or malformed entry should never prevent the page from loading.
  }
}

function applyPostNavigationState(state) {
  const detailsByKey = new Map((state.details || []).map(item => [item.key, item.open]));
  [...document.querySelectorAll("details")]
    .filter(detail => !detail.closest(".nav-popover") && !detail.matches("[data-no-post-restore]"))
    .forEach((detail, index) => {
      const open = detailsByKey.get(postNavigationDetailsKey(detail, index));
      if (open !== undefined) detail.open = open;
    });

  for (const id of state.openDialogs || []) {
    const dialog = document.getElementById(id);
    if (dialog instanceof HTMLDialogElement && !dialog.open) dialog.showModal();
  }

  const focused = state.focusedSelector ? document.querySelector(state.focusedSelector) : null;
  if (focused instanceof HTMLElement) focused.focus({ preventScroll: true });

  requestAnimationFrame(() => requestAnimationFrame(() => {
    window.scrollTo({ left: state.scrollX || 0, top: state.scrollY || 0, behavior: "auto" });
  }));
}

function postNavigationFocusSelector(element) {
  if (!(element instanceof HTMLElement)) return null;
  if (element.id) return `#${CSS.escape(element.id)}`;

  const name = element.getAttribute("name");
  if (!name) return null;
  const namedSelector = `${element.localName}[name="${CSS.escape(name)}"]`;
  const containingRow = element.closest("[id]");
  if (containingRow?.id) return `#${CSS.escape(containingRow.id)} ${namedSelector}`;
  return document.querySelectorAll(namedSelector).length === 1 ? namedSelector : null;
}

function postNavigationDetailsKey(detail, index) {
  if (detail.id) return `id:${detail.id}`;
  if (detail.dataset.stateKey) return `state:${detail.dataset.stateKey}`;

  const summary = detail.querySelector(":scope > summary")?.innerText?.replace(/\s+/g, " ").trim();
  return `details:${index}:${summary || ""}`;
}

async function downloadPostResponse(response, disposition) {
  const blob = await response.blob();
  const match = disposition.match(/filename\*?=(?:UTF-8'')?["']?([^;"']+)/i);
  const filename = match ? decodeURIComponent(match[1].trim()) : "download";
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

window.showBingoToast = (message, type = "success") => {
  let host = document.querySelector("#app-notice-region");
  if (!(host instanceof HTMLElement)) {
    host = document.createElement("div");
    host.id = "app-notice-region";
    host.setAttribute("aria-live", "polite");
    host.setAttribute("aria-atomic", "true");
    document.body.appendChild(host);
  }
  const ownerDialog = host.closest("dialog");
  if (ownerDialog instanceof HTMLDialogElement && !ownerDialog.open) document.body.append(host);
  const toast = createTransientToast(message, type);
  host.appendChild(toast);
  initializeTransientToast(toast);
};

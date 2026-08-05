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
  const { onChange: changed, onReady: ready, timeLabel = "Time", ...options } = overrides;
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
        hourSelect.setAttribute("aria-label", timeLabel + " hour");
        window.bingoDateTimeHours().forEach((hour) => hourSelect.add(new Option(hour, hour)));
        minuteSelect = document.createElement("select");
        minuteSelect.className = "event-time-select";
        minuteSelect.setAttribute("aria-label", timeLabel + " minute");
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

document.addEventListener("DOMContentLoaded", () => {
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
  const feedback = document.querySelector("[data-feedback-target], .validation-summary-errors");
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
        timeLabel: input.dataset.timeLabel || "Time"
      });
    });
  }

  const menus = [...document.querySelectorAll(".nav-popover details")];
  for (const menu of menus) {
    menu.addEventListener("toggle", () => {
      if (!menu.open) return;
      for (const other of menus) if (other !== menu) other.open = false;
    });
    if (menu.closest("[data-admin-event-selector]")) {
      menu.addEventListener("keydown", event => {
        if (event.key !== "Escape") return;
        event.preventDefault();
        menu.open = false;
        menu.querySelector("summary")?.focus();
      });
    }
  }
  document.addEventListener("click", event => {
    const dismissButton = event.target.closest("[data-dismiss-notice]");
    if (dismissButton) dismissButton.closest(".app-notice")?.remove();

    for (const menu of menus) if (menu.open && !menu.contains(event.target)) menu.open = false;
  });
});

function initializeAdminMenu() {
  const toggle = document.querySelector("[data-admin-menu-toggle]");
  const sidebar = toggle instanceof HTMLButtonElement ? document.getElementById(toggle.getAttribute("aria-controls")) : null;
  const scrim = document.querySelector("[data-admin-menu-scrim]");
  if (!(toggle instanceof HTMLButtonElement) || !(sidebar instanceof HTMLElement) || !(scrim instanceof HTMLButtonElement)) return;

  document.documentElement.classList.add("admin-menu-enhanced");
  let open = false;

  const setOpen = (next, restoreFocus = false) => {
    open = next;
    sidebar.classList.toggle("is-open", open);
    sidebar.setAttribute("aria-hidden", String(!open && window.innerWidth <= 900));
    toggle.setAttribute("aria-expanded", String(open));
    scrim.hidden = !open;
    document.body.classList.toggle("admin-menu-open", open);
    if (open) {
      sidebar.querySelector("a")?.focus({ preventScroll: true });
    } else if (restoreFocus) {
      toggle.focus({ preventScroll: true });
    }
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
    if (event.key === "Escape" && open) {
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
  const state = form?.querySelector("[data-admin-event-state-filter]");
  const table = page?.querySelector("[data-admin-event-table]");
  const empty = page?.querySelector("[data-admin-event-filter-empty]");
  if (!(form instanceof HTMLFormElement) || !(input instanceof HTMLInputElement) || !(state instanceof HTMLSelectElement) || !(table instanceof HTMLTableElement) || !(empty instanceof HTMLElement)) return;

  const apply = () => {
    const search = input.value.trim().toLowerCase();
    const selectedState = state.value.toLowerCase();
    let visible = 0;
    table.querySelectorAll("[data-admin-event-row]").forEach(row => {
      const matchesSearch = !search || row.dataset.eventName?.toLowerCase().includes(search) || row.dataset.eventSlug?.toLowerCase().includes(search);
      const matchesState = selectedState === "all" || row.dataset.eventState === selectedState;
      row.hidden = !(matchesSearch && matchesState);
      if (!row.hidden) visible++;
    });
    table.hidden = visible === 0;
    empty.hidden = visible > 0;
  };

  input.addEventListener("input", apply);
  state.addEventListener("change", apply);
  form.addEventListener("submit", event => {
    event.preventDefault();
    apply();
  });
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
    if (!(region instanceof HTMLElement) || !region.classList.contains("auto-hide-scrollbar")) return;

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
      window.alert("The change could not be sent. Check your connection and try again.");
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
  let stack = document.querySelector("[data-bingo-toast-stack]");
  if (!stack) {
    stack = document.createElement("div");
    stack.className = "app-toast-stack";
    stack.dataset.bingoToastStack = "true";
    stack.setAttribute("aria-live", "polite");
    document.body.appendChild(stack);
  }
  const toast = document.createElement("div");
  toast.className = `app-toast app-toast-${type}`;
  toast.setAttribute("role", type === "error" ? "alert" : "status");
  toast.textContent = message;
  stack.appendChild(toast);
  window.setTimeout(() => toast.remove(), type === "error" ? 8000 : 5000);
};

// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
initializeInputModality();

document.addEventListener("DOMContentLoaded", () => {
  restorePostNavigationState();
  initializePostNavigation();
  initializeAutoHideScrollbars();
  const feedback = document.querySelector("[data-feedback-target], .validation-summary-errors");
  if (feedback) {
    feedback.scrollIntoView({ block: "nearest" });
    feedback.focus({ preventScroll: true });
  }

  if (typeof window.flatpickr === "function") {
    document.querySelectorAll("[data-date-time-picker]").forEach((input) => {
      window.flatpickr(input, {
        enableTime: true,
        enableSeconds: true,
        time_24hr: true,
        dateFormat: "Y-m-d H:i:S",
        altInput: true,
        altFormat: "d/m/Y H:i:S",
        defaultDate: input.value || null,
        minDate: input.dataset.minDate || null,
        maxDate: input.dataset.maxDate || null,
        minuteIncrement: 1,
        disableMobile: true,
        allowInput: false
      });
    });
  }

  const menus = [...document.querySelectorAll(".nav-popover details")];
  for (const menu of menus) {
    menu.addEventListener("toggle", () => {
      if (!menu.open) return;
      for (const other of menus) if (other !== menu) other.open = false;
    });
  }
  document.addEventListener("click", event => {
    const dismissButton = event.target.closest("[data-dismiss-notice]");
    if (dismissButton) dismissButton.closest(".app-notice")?.remove();

    for (const menu of menus) if (menu.open && !menu.contains(event.target)) menu.open = false;
  });
});

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
      const headers = { "X-Requested-With": "XMLHttpRequest" };
      if (!form.dataset.updateTargets) headers["X-Bingo-Enhanced-Post"] = "true";

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

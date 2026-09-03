(() => {
  const content = document.querySelector(".public-team-board");
  if (!(content instanceof HTMLElement)) return;

  let drawerDirty = false;
  const desktopLayout = window.matchMedia("(min-width: 901px)");

  function reducedMotion() {
    return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  }

  function handlerUrl(href, handler) {
    const url = new URL(href, window.location.href);
    url.searchParams.set("handler", handler);
    return url;
  }

  async function fetchHtml(url) {
    const response = await fetch(url, {
      credentials: "same-origin",
      headers: { "X-Requested-With": "XMLHttpRequest" }
    });
    if (!response.ok) throw new Error(`Request failed with ${response.status}.`);
    return response.text();
  }

  let sidebarRequest = 0;
  let activeSidebarUrl = window.location.href;

  function focusSidebar(sidebar, focusHref) {
    let target = sidebar.querySelector("[data-team-sidebar-return]");
    if (focusHref) {
      const previousPath = new URL(focusHref, window.location.href).pathname;
      target = [...content.querySelectorAll("a.public-ui-team-board-tile")]
        .find(link => new URL(link.href, window.location.href).pathname === previousPath) ?? target;
    }
    target?.focus?.({ preventScroll: true });
  }

  async function replaceSidebar(href, focusHref) {
    const request = ++sidebarRequest;
    content.setAttribute("aria-busy", "true");
    try {
      const parsedDocument = new DOMParser().parseFromString(await fetchHtml(new URL(href, window.location.href)), "text/html");
      const current = content.querySelector(".public-team-sidebar");
      const replacement = parsedDocument.querySelector(".public-team-sidebar");
      if (!(current instanceof HTMLElement) || !(replacement instanceof HTMLElement)) throw new Error("Team sidebar was missing from the response.");
      if (request !== sidebarRequest) return true;
      current.replaceWith(replacement);
      activeSidebarUrl = href;
      refreshWorkspaceGeometry();
      focusSidebar(replacement, focusHref);
      return true;
    } catch {
      return false;
    } finally {
      if (request === sidebarRequest) content.removeAttribute("aria-busy");
    }
  }

  async function navigateSidebar(link) {
    const href = link.href;
    const focusHref = link.matches("[data-team-sidebar-return]") ? activeSidebarUrl : null;
    if (await replaceSidebar(href, focusHref)) {
      window.history.pushState({ teamBoardSidebar: true }, "", href);
      if (link.matches("a.public-ui-team-board-tile") && !desktopLayout.matches) {
        content.querySelector(".tile-context-sidebar")?.scrollIntoView({ behavior: reducedMotion() ? "auto" : "smooth", block: "start" });
      }
    } else {
      window.location.assign(href);
    }
  }

  async function restoreSidebarFromHistory() {
    if (content.querySelector("[data-submission-drawer]")) {
      window.location.reload();
      return;
    }
    const previousUrl = activeSidebarUrl;
    if (!(await replaceSidebar(window.location.href, previousUrl))) window.location.reload();
  }

  let boardResizeObserver;
  let geometryFrame;
  function clearWorkspaceGeometry() {
    const workspace = content.querySelector(".public-team-workspace");
    workspace?.style.removeProperty("--public-team-board-row-height");
    workspace?.style.removeProperty("--public-team-board-row-top");
  }

  function syncWorkspaceGeometry() {
    const workspace = content.querySelector(".public-team-workspace");
    const board = workspace?.querySelector(".public-full-board");
    if (!(workspace instanceof HTMLElement) || !(board instanceof HTMLElement) || !desktopLayout.matches) {
      clearWorkspaceGeometry();
      return;
    }
    const boardRect = board.getBoundingClientRect();
    const workspaceRect = workspace.getBoundingClientRect();
    workspace.style.setProperty("--public-team-board-row-height", `${boardRect.height}px`);
    workspace.style.setProperty("--public-team-board-row-top", `${boardRect.top - workspaceRect.top}px`);
  }

  function refreshWorkspaceGeometry() {
    boardResizeObserver?.disconnect();
    boardResizeObserver = undefined;
    if (!desktopLayout.matches) {
      clearWorkspaceGeometry();
      return;
    }
    const board = content.querySelector(".public-full-board");
    if (board instanceof HTMLElement && "ResizeObserver" in window) {
      boardResizeObserver = new ResizeObserver(() => syncWorkspaceGeometry());
      boardResizeObserver.observe(board);
    }
    if (geometryFrame) cancelAnimationFrame(geometryFrame);
    geometryFrame = requestAnimationFrame(() => {
      geometryFrame = undefined;
      syncWorkspaceGeometry();
    });
  }

  function removeDrawer(force = false) {
    const drawer = content.querySelector("[data-submission-drawer]");
    if (!(drawer instanceof HTMLElement)) return true;
    if (!force && drawerDirty && !window.confirm(drawer.dataset.submissionDiscardConfirm || "")) return false;
    const finish = () => {
      drawer.remove();
      content.classList.remove("public-ui-submission-drawer-open");
      drawerDirty = false;
    };
    if (force || reducedMotion()) { finish(); return true; }
    drawer.classList.add("closing");
    const animation = drawer.animate([{ transform: "translateX(0)" }, { transform: "translateX(-100%)" }], { duration: 300, easing: "cubic-bezier(0.4, 0, 0.6, 1)", fill: "forwards" });
    animation.finished.then(finish, finish);
    return true;
  }

  async function openDrawer(link) {
    if (content.querySelector("[data-submission-drawer]")) return;
    content.classList.add("public-ui-submission-drawer-loading");
    try {
      const template = document.createElement("template");
      template.innerHTML = (await fetchHtml(handlerUrl(link.href, "Drawer"))).trim();
      const drawer = template.content.querySelector("[data-submission-drawer]");
      if (!(drawer instanceof HTMLElement)) throw new Error("Submission drawer was missing from the response.");
      const sidebar = content.querySelector(".public-team-sidebar");
      drawer.classList.add("preparing");
      content.classList.add("public-ui-submission-drawer-open");
      if (sidebar instanceof HTMLElement) sidebar.after(drawer);
      else content.append(drawer);
      refreshWorkspaceGeometry();
      await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
      drawer.classList.remove("preparing");
      if (!desktopLayout.matches) drawer.scrollIntoView({ behavior: reducedMotion() ? "auto" : "smooth", block: "start" });
      if (!reducedMotion()) {
        drawer.animate([{ transform: "translateX(-100%)" }, { transform: "translateX(0)" }], { duration: 300, easing: "cubic-bezier(0.22, 1, 0.36, 1)" });
      }
      window.publicLeaderboards?.initialize?.(drawer);
      (drawer.querySelector("[data-public-ui-select-dropdown] > summary") ?? drawer.querySelector("select, input, textarea, button"))?.focus({ preventScroll: true });
    } catch {
      content.classList.remove("public-ui-submission-drawer-open");
      window.location.assign(link.href);
    } finally {
      content.classList.remove("public-ui-submission-drawer-loading");
    }
  }

  async function selectTile(select) {
    const drawer = select.closest("[data-submission-drawer]");
    const option = select.selectedOptions[0];
    const drawerUrl = option?.dataset.drawerUrl;
    if (!(drawer instanceof HTMLElement) || !drawerUrl) return;
    content.classList.add("public-ui-submission-drawer-loading");
    try {
      const template = document.createElement("template");
      template.innerHTML = (await fetchHtml(new URL(drawerUrl, window.location.href))).trim();
      const replacement = template.content.querySelector("[data-submission-drawer]");
      if (!(replacement instanceof HTMLElement)) throw new Error("submission-refresh-failed");
      drawer.replaceWith(replacement);
      window.publicLeaderboards?.initialize?.(replacement);
      drawerDirty = false;
      (replacement.querySelector("[data-submission-tile-select]")?.closest("[data-public-ui-select-dropdown]")?.querySelector("summary") ?? replacement.querySelector("[data-submission-tile-select]"))?.focus({ preventScroll: true });
    } catch {
      select.value = "";
    } finally {
      content.classList.remove("public-ui-submission-drawer-loading");
    }
  }

  async function submitDrawer(form) {
    const drawer = form.closest("[data-submission-drawer]");
    const submit = form.querySelector("button[type='submit'], button:not([type])");
    if (!(drawer instanceof HTMLElement)) return;
    submit?.setAttribute("disabled", "");
    try {
      const response = await fetch(form.action, { method: "POST", body: new FormData(form), credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } });
      const type = response.headers.get("content-type") ?? "";
      if (type.includes("application/json")) {
        const result = await response.json();
        if (!response.ok || !result.success) throw new Error(result.message ?? drawer.dataset.submissionFailed ?? "");
        showResult(drawer, true, result.message ?? drawer.dataset.submissionSuccess ?? "");
        drawerDirty = false;
        return;
      }
      const template = document.createElement("template");
      template.innerHTML = (await response.text()).trim();
      const replacement = template.content.querySelector("[data-submission-drawer]");
      if (!(replacement instanceof HTMLElement)) throw new Error("submission-refresh-failed");
      drawer.replaceWith(replacement);
      window.publicLeaderboards?.initialize?.(replacement);
      drawerDirty = true;
      showResult(replacement, false, replacement.querySelector(".public-ui-submission-validation")?.textContent?.trim() || replacement.dataset.submissionValidationFailed || "");
    } catch (error) {
      showResult(drawer, false, drawer.dataset.submissionRetryFailed || "");
    }
  }

  function showResult(drawer, successful, message) {
    drawer.querySelector("[data-submission-result]")?.remove();
    drawer.classList.add("public-ui-submission-result-open");
    const result = document.createElement("div");
    result.className = `public-ui-submission-result ${successful ? "public-ui-submission-result--success" : "public-ui-submission-result--failure"}`;
    result.dataset.submissionResult = "";
    result.setAttribute("role", successful ? "status" : "alert");
    const mark = document.createElement("span");
    mark.className = `public-ui-state ${successful ? "public-ui-state--success" : "public-ui-state--error"}`;
    mark.textContent = successful ? "✓" : "!";
    const heading = document.createElement("strong");
    heading.className = "public-ui-component-title";
    heading.textContent = successful ? drawer.dataset.submissionSuccessTitle || "" : drawer.dataset.submissionFailureTitle || "";
    const copy = document.createElement("p");
    copy.className = "public-ui-supporting-text";
    copy.textContent = message;
    const action = document.createElement("button");
    action.type = "button";
    action.className = `public-ui-action ${successful ? "public-ui-action--commit" : "public-ui-action--standard"}`;
    action.textContent = successful ? drawer.dataset.submissionOk || "" : drawer.dataset.submissionRetry || "";
    action.addEventListener("click", () => {
      if (successful) { removeDrawer(); return; }
      result.remove();
      drawer.classList.remove("public-ui-submission-result-open");
      drawer.querySelector("button[type='submit'], button:not([type])")?.removeAttribute("disabled");
    });
    result.append(mark, heading, copy, action);
    drawer.append(result);
    action.focus({ preventScroll: true });
  }

  desktopLayout.addEventListener?.("change", refreshWorkspaceGeometry);
  window.addEventListener("resize", refreshWorkspaceGeometry);
  content.addEventListener("click", event => {
    const target = event.target instanceof Element ? event.target : null;
    const navigationLink = target?.closest("a.public-ui-team-board-tile, a[data-team-sidebar-return]");
    if (navigationLink instanceof HTMLAnchorElement && event.button === 0 && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey && !content.querySelector("[data-submission-drawer]")) {
      event.preventDefault();
      navigateSidebar(navigationLink);
      return;
    }
    const submitLink = target?.closest("[data-open-submission-drawer]");
    if (submitLink instanceof HTMLAnchorElement) {
      event.preventDefault();
      openDrawer(submitLink);
    } else if (target?.closest("[data-submission-drawer-close]")) {
      removeDrawer();
    }
  });
  content.addEventListener("input", event => {
    if (event.target instanceof Element && event.target.closest("[data-submission-drawer]")) drawerDirty = true;
  });
  content.addEventListener("change", event => {
    const target = event.target;
    if (!(target instanceof Element) || !target.closest("[data-submission-drawer]")) return;
    if (target instanceof HTMLSelectElement && target.matches("[data-submission-tile-select]")) return selectTile(target);
    if (target instanceof HTMLSelectElement && target.matches("[data-submission-target-select]")) {
      const drawer = target.closest("[data-submission-drawer]");
      const option = target.selectedOptions[0];
      const requirement = drawer?.querySelector("[data-submission-requirement-id]");
      const drop = drawer?.querySelector("[data-submission-drop-id]");
      if (requirement instanceof HTMLInputElement) requirement.value = option?.dataset.requirementId ?? "";
      if (drop instanceof HTMLInputElement) drop.value = option?.dataset.dropId ?? "";
    }
    drawerDirty = true;
  });
  content.addEventListener("submit", event => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement) || !form.closest("[data-submission-drawer]")) return;
    event.preventDefault();
    submitDrawer(form);
  });
  window.addEventListener("popstate", restoreSidebarFromHistory);
  refreshWorkspaceGeometry();

  if (new URLSearchParams(window.location.search).get("submission")?.toLowerCase() === "true") {
    requestAnimationFrame(() => content.querySelector("[data-open-submission-drawer]")?.click());
  }
})();

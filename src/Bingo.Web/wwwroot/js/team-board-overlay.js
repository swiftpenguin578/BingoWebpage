(() => {
  const dialog = document.querySelector("[data-team-board-overlay]");
  const content = dialog?.querySelector("[data-team-board-overlay-content]");
  const closeButton = dialog?.querySelector("[data-team-board-overlay-close]");
  const desktop = window.matchMedia("(min-width: 901px)");
  if (!(dialog instanceof HTMLDialogElement) || !(content instanceof HTMLElement)) return;

  const overviewUrl = window.location.href;
  const restoreKey = "team-board-overlay-restore";
  let requestController = null;
  let requestGeneration = 0;
  let pending = false;
  let closing = false;
  let panelAnimation = null;
  let currentTeamUrl = null;
  let currentTileUrl = null;
  let teamSidebar = null;
  let drawerDirty = false;
  let evidenceResizeObserver = null;
  const boardCache = new Map();

  function reducedMotion() {
    return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  }

  function publicUrl(href) {
    const url = new URL(href, window.location.href);
    url.searchParams.delete("overlay");
    url.searchParams.delete("handler");
    return url;
  }

  function handlerUrl(href, handler) {
    const url = new URL(href, window.location.href);
    url.searchParams.set("handler", handler);
    return url;
  }

  function overlayUrl(href) {
    const url = publicUrl(href);
    url.searchParams.set("overlay", "true");
    return url;
  }

  function saveRestoreState() {
    if (!currentTeamUrl) return;
    try {
      window.sessionStorage.setItem(restoreKey, JSON.stringify({
        overviewUrl,
        teamUrl: currentTeamUrl.href,
        tileUrl: currentTileUrl?.href ?? null
      }));
    } catch { /* Session storage is an enhancement only. */ }
  }

  function clearRestoreState() {
    try { window.sessionStorage.removeItem(restoreKey); }
    catch { /* Session storage is an enhancement only. */ }
  }

  async function fetchHtml(url, signal) {
    const response = await fetch(url, {
      credentials: "same-origin",
      headers: { "X-Requested-With": "XMLHttpRequest" },
      signal
    });
    if (!response.ok) throw new Error(`Request failed with ${response.status}.`);
    return response.text();
  }

  async function fetchWorkspace(url, signal) {
    const cachedBoard = boardCache.get(url.href);
    if (cachedBoard) return cachedBoard;

    let lastError;
    for (let attempt = 0; attempt < 2; attempt++) {
      try {
        const parsed = new DOMParser().parseFromString(await fetchHtml(url, signal), "text/html");
        const board = parsed.querySelector(".public-team-board");
        if (!(board instanceof HTMLElement)) throw new Error("Team board was missing from the response.");
        boardCache.set(url.href, board);
        return board;
      } catch (error) {
        if (error?.name === "AbortError") throw error;
        lastError = error;
        if (attempt === 0) await new Promise(resolve => window.setTimeout(resolve, 150));
      }
    }
    throw lastError;
  }

  async function prepareImages(container) {
    const images = Array.from(container.querySelectorAll("img"));
    await Promise.allSettled(images.map(image => {
      image.loading = "eager";
      if (image.complete) return image.decode?.() ?? Promise.resolve();
      return new Promise(resolve => {
        image.addEventListener("load", resolve, { once: true });
        image.addEventListener("error", resolve, { once: true });
      });
    }));
  }

  function boardTiles() {
    return Array.from(content.querySelectorAll("a.public-tile"));
  }

  function markSelectedTile(url) {
    boardTiles().forEach(tile => {
      const selected = publicUrl(tile.href).pathname === url?.pathname;
      tile.classList.toggle("is-selected", selected);
      if (selected) tile.setAttribute("aria-current", "page");
      else tile.removeAttribute("aria-current");
    });
  }

  function fitEvidenceRows(sidebar) {
    const section = sidebar.querySelector(".tile-context-evidence");
    const grid = section?.querySelector(".tile-evidence-thumbnails");
    const firstCard = grid?.firstElementChild;
    if (!(section instanceof HTMLElement) || !(grid instanceof HTMLElement) || !(firstCard instanceof HTMLElement)) return;

    const sectionStyle = window.getComputedStyle(section);
    const gridStyle = window.getComputedStyle(grid);
    const header = section.querySelector(":scope > header");
    const available = section.clientHeight
      - Number.parseFloat(sectionStyle.paddingTop)
      - Number.parseFloat(sectionStyle.paddingBottom)
      - (header instanceof HTMLElement ? header.offsetHeight + Number.parseFloat(window.getComputedStyle(header).marginBottom) : 0);
    const rowHeight = firstCard.getBoundingClientRect().height;
    const gap = Number.parseFloat(gridStyle.rowGap) || 0;
    const rows = Math.max(1, Math.floor((available + gap) / (rowHeight + gap)));
    grid.style.flex = "0 0 auto";
    grid.style.height = `${rows * rowHeight + Math.max(0, rows - 1) * gap}px`;
  }

  function observeEvidenceRows(sidebar) {
    evidenceResizeObserver?.disconnect();
    evidenceResizeObserver = null;
    const section = sidebar.querySelector(".tile-context-evidence");
    if (!(section instanceof HTMLElement) || !section.querySelector(".tile-evidence-thumbnails")) return;
    evidenceResizeObserver = new ResizeObserver(() => fitEvidenceRows(sidebar));
    evidenceResizeObserver.observe(section);
    window.requestAnimationFrame(() => fitEvidenceRows(sidebar));
  }

  function removeSubmissionDrawer(force = false) {
    const drawer = content.querySelector("[data-submission-drawer]");
    if (!drawer) return true;
    if (drawer.classList.contains("closing")) return true;
    if (!force && drawerDirty && !window.confirm("Discard the evidence submission you started?")) return false;
    drawerDirty = false;
    const scrim = content.querySelector("[data-submission-drawer-scrim]");
    const finish = () => {
      drawer.remove();
      scrim?.remove();
      content.classList.remove("submission-drawer-open");
    };
    if (force || reducedMotion()) {
      finish();
      return true;
    }

    drawer.classList.add("closing");
    scrim?.classList.add("closing");
    drawer.getAnimations().forEach(animation => animation.cancel());
    scrim?.getAnimations().forEach(animation => animation.cancel());
    const drawerAnimation = drawer.animate(
      [
        { opacity: 1, transform: "translateX(0)" },
        { opacity: 0, transform: "translateX(-100%)" }
      ],
      { duration: 300, easing: "cubic-bezier(0.4, 0, 0.6, 1)", fill: "forwards" }
    );
    scrim?.animate(
      [{ opacity: 1 }, { opacity: 0 }],
      { duration: 300, easing: "cubic-bezier(0.4, 0, 0.6, 1)", fill: "forwards" }
    );
    drawerAnimation.finished.then(finish, finish);
    return true;
  }

  function restoreTeamSidebar(updateHistory) {
    if (!teamSidebar || !removeSubmissionDrawer()) return;
    evidenceResizeObserver?.disconnect();
    evidenceResizeObserver = null;
    content.querySelector("[data-tile-context-sidebar]")?.replaceWith(teamSidebar.cloneNode(true));
    currentTileUrl = null;
    markSelectedTile(null);
    saveRestoreState();
    if (updateHistory) window.history.back();
  }

  async function openTileSidebar(href, historyMode = "push") {
    if (!dialog.open || !currentTeamUrl) return;
    const tileUrl = publicUrl(href);
    const generation = ++requestGeneration;
    markSelectedTile(tileUrl);
    content.classList.add("tile-context-loading");

    try {
      const html = await fetchHtml(handlerUrl(tileUrl, "Sidebar"));
      if (generation !== requestGeneration || !dialog.open) return;
      const template = document.createElement("template");
      template.innerHTML = html.trim();
      const sidebar = template.content.querySelector("[data-tile-context-sidebar]");
      if (!(sidebar instanceof HTMLElement)) throw new Error("Tile sidebar was missing from the response.");

      removeSubmissionDrawer(true);
      content.querySelector(".public-team-sidebar")?.replaceWith(sidebar);
      currentTileUrl = tileUrl;
      observeEvidenceRows(sidebar);
      saveRestoreState();
      const state = { teamBoardOverlay: true, tileSidebar: true };
      if (historyMode === "push") window.history.pushState(state, "", tileUrl);
      else if (historyMode === "replace") window.history.replaceState(state, "", tileUrl);
    } catch {
      if (generation !== requestGeneration) return;
      markSelectedTile(currentTileUrl);
    } finally {
      if (generation === requestGeneration) content.classList.remove("tile-context-loading");
    }
  }

  async function openSubmissionDrawer(link) {
    if (content.querySelector("[data-submission-drawer]")) return;
    content.classList.add("submission-drawer-loading");
    try {
      const html = await fetchHtml(handlerUrl(link.href, "Drawer"));
      const template = document.createElement("template");
      template.innerHTML = html.trim();
      const drawer = template.content.querySelector("[data-submission-drawer]");
      if (!(drawer instanceof HTMLElement)) throw new Error("Submission drawer was missing from the response.");
      const activeSidebar = content.querySelector("[data-tile-context-sidebar], .public-team-sidebar");
      if (activeSidebar instanceof HTMLElement) {
        drawer.style.height = `${activeSidebar.getBoundingClientRect().height}px`;
      }
      const scrim = document.createElement("div");
      scrim.className = "submission-drawer-scrim";
      scrim.dataset.submissionDrawerScrim = "";
      drawer.classList.add("preparing");
      scrim.classList.add("preparing");
      content.classList.add("submission-drawer-open");
      content.append(scrim, drawer);
      await new Promise(resolve => window.requestAnimationFrame(
        () => window.requestAnimationFrame(resolve)
      ));
      drawer.classList.remove("preparing");
      scrim.classList.remove("preparing");
      if (!reducedMotion()) {
        drawer.animate(
          [
            { opacity: 0, transform: "translateX(-100%)" },
            { opacity: 1, transform: "translateX(0)" }
          ],
          { duration: 300, easing: "cubic-bezier(0.22, 1, 0.36, 1)" }
        );
        scrim.animate(
          [{ opacity: 0 }, { opacity: 1 }],
          { duration: 300, easing: "cubic-bezier(0.22, 1, 0.36, 1)" }
        );
      }
      drawer.querySelector("select, input, textarea, button")?.focus({ preventScroll: true });
    } catch {
      content.classList.remove("submission-drawer-open");
      window.location.assign(link.href);
    } finally {
      content.classList.remove("submission-drawer-loading");
    }
  }

  async function submitDrawerForm(form) {
    const submit = form.querySelector("button[type='submit'], button:not([type])");
    const status = form.querySelector("[data-submission-status]") ?? document.createElement("p");
    status.dataset.submissionStatus = "";
    status.className = "submission-drawer-status";
    if (!status.isConnected) form.append(status);
    submit?.setAttribute("disabled", "");
    status.textContent = "Submitting evidence…";

    function showResult(drawer, successful, message) {
      drawer.querySelector("[data-submission-result]")?.remove();
      drawer.classList.add("submission-result-open");
      drawer.scrollTop = 0;
      const result = document.createElement("div");
      result.className = `submission-result ${successful ? "success" : "failure"}`;
      result.dataset.submissionResult = "";
      result.setAttribute("role", successful ? "status" : "alert");

      const mark = document.createElement("span");
      mark.className = "submission-result-mark";
      mark.textContent = successful ? "✓" : "!";
      const heading = document.createElement("strong");
      heading.textContent = successful ? "Drop submitted" : "Submission failed";
      const copy = document.createElement("p");
      copy.textContent = message;
      const action = document.createElement("button");
      action.type = "button";
      action.className = successful ? "btn action-accent-outline" : "btn action-neutral-outline";
      action.textContent = successful ? "Okay" : "Try again";
      action.addEventListener("click", () => {
        if (successful) {
          removeSubmissionDrawer();
          return;
        }
        result.remove();
        drawer.classList.remove("submission-result-open");
        submit?.removeAttribute("disabled");
        status.textContent = "";
        form.querySelector("[aria-invalid='true'], select, input, textarea, button")?.focus({ preventScroll: true });
      });

      result.append(mark, heading, copy, action);
      drawer.append(result);
      action.focus({ preventScroll: true });
    }

    try {
      const response = await fetch(form.action, {
        method: "POST",
        body: new FormData(form),
        credentials: "same-origin",
        headers: { "X-Requested-With": "XMLHttpRequest" }
      });
      const type = response.headers.get("content-type") ?? "";
      if (type.includes("application/json")) {
        const result = await response.json();
        if (!response.ok || !result.success) throw new Error(result.message ?? "Submission failed.");
        drawerDirty = false;
        const drawer = form.closest("[data-submission-drawer]");
        if (!(drawer instanceof HTMLElement)) throw new Error("The submission result could not be displayed.");
        showResult(drawer, true, result.message ?? "Your drop was submitted and is awaiting review.");
        return;
      }

      const html = await response.text();
      const template = document.createElement("template");
      template.innerHTML = html.trim();
      const replacement = template.content.querySelector("[data-submission-drawer]");
      const drawer = content.querySelector("[data-submission-drawer]");
      if (!(replacement instanceof HTMLElement) || !(drawer instanceof HTMLElement)) throw new Error("Submission form could not be refreshed.");
      replacement.style.height = drawer.style.height;
      drawer.replaceWith(replacement);
      drawerDirty = true;
      const validationMessage = replacement.querySelector(".validation-summary")?.textContent?.trim()
        || "Check the highlighted fields and try again.";
      showResult(replacement, false, validationMessage);
    } catch (error) {
      const drawer = form.closest("[data-submission-drawer]");
      const message = error instanceof Error ? error.message : "Submission failed. Please try again.";
      if (drawer instanceof HTMLElement) showResult(drawer, false, message);
      else {
        status.textContent = message;
        submit?.removeAttribute("disabled");
      }
    }
  }

  async function openOverlay(link, restoredTileUrl = null) {
    if (pending || closing || dialog.open) return;

    const generation = ++requestGeneration;
    const requestedUrl = overlayUrl(link.href);
    currentTeamUrl = publicUrl(link.href);
    currentTileUrl = null;
    saveRestoreState();
    requestController = new AbortController();
    pending = true;
    window.history.pushState({ teamBoardOverlay: true }, "", currentTeamUrl);

    try {
      const board = await fetchWorkspace(requestedUrl, requestController.signal);
      if (generation !== requestGeneration) return;

      content.replaceChildren(document.importNode(board, true));
      teamSidebar = content.querySelector(".public-team-sidebar")?.cloneNode(true) ?? null;
      await prepareImages(content);
      if (generation !== requestGeneration) return;
      pending = false;
      requestController = null;
      dialog.showModal();
      document.body.classList.add("team-board-overlay-open");
      if (restoredTileUrl) await openTileSidebar(restoredTileUrl, "push");
      else closeButton?.focus({ preventScroll: true });
    } catch (error) {
      if (generation !== requestGeneration || error?.name === "AbortError") return;
      pending = false;
      requestController = null;
      currentTeamUrl = null;
      clearRestoreState();
      window.history.replaceState(null, "", overviewUrl);
    }
  }

  function finishClose(updateHistory, historyDistance) {
    closing = false;
    const completedAnimation = panelAnimation;
    panelAnimation = null;
    completedAnimation?.cancel();
    dialog.close();
    dialog.classList.remove("closing");
    dialog.style.removeProperty("opacity");
    content.replaceChildren();
    document.body.classList.remove("team-board-overlay-open");
    currentTeamUrl = null;
    currentTileUrl = null;
    teamSidebar = null;
    drawerDirty = false;
    evidenceResizeObserver?.disconnect();
    evidenceResizeObserver = null;
    clearRestoreState();
    if (updateHistory) window.history.go(-historyDistance);
  }

  function closeOverlay(updateHistory) {
    const historyDistance = currentTileUrl ? 2 : 1;
    if (pending) {
      ++requestGeneration;
      requestController?.abort();
      requestController = null;
      pending = false;
      content.replaceChildren();
      currentTeamUrl = null;
      currentTileUrl = null;
      clearRestoreState();
      if (updateHistory) window.history.go(-historyDistance);
      return;
    }
    if (!dialog.open || closing || !removeSubmissionDrawer()) return;

    closing = true;
    dialog.classList.add("closing");
    panelAnimation?.cancel();
    if (reducedMotion()) {
      finishClose(updateHistory, historyDistance);
      return;
    }

    panelAnimation = dialog.animate(
      [{ opacity: 1 }, { opacity: 0 }],
      { duration: 190, easing: "ease-out", fill: "forwards" }
    );
    panelAnimation.finished.then(
      () => { if (closing) finishClose(updateHistory, historyDistance); },
      () => { if (closing) finishClose(updateHistory, historyDistance); }
    );
  }

  document.querySelectorAll(".mission-team-card").forEach(link => {
    link.addEventListener("click", event => {
      if (!desktop.matches || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      event.preventDefault();
      event.stopImmediatePropagation();
      openOverlay(link);
    }, { capture: true });
  });

  content.addEventListener("click", event => {
    const target = event.target instanceof Element ? event.target : null;
    const tile = target?.closest("a.public-tile");
    if (tile instanceof HTMLAnchorElement) {
      if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      event.preventDefault();
      openTileSidebar(tile.href, currentTileUrl ? "replace" : "push");
      return;
    }

    if (target?.closest("[data-team-sidebar-return]")) {
      restoreTeamSidebar(true);
      return;
    }

    const submitLink = target?.closest("[data-open-submission-drawer]");
    if (submitLink instanceof HTMLAnchorElement) {
      event.preventDefault();
      openSubmissionDrawer(submitLink);
      return;
    }

    if (target?.closest("[data-submission-drawer-close]") || target?.matches("[data-submission-drawer-scrim]")) {
      removeSubmissionDrawer();
    }
  });

  content.addEventListener("input", event => {
    if (event.target instanceof Element && event.target.closest("[data-submission-drawer]")) drawerDirty = true;
  });
  content.addEventListener("change", event => {
    const target = event.target;
    if (!(target instanceof Element) || !target.closest("[data-submission-drawer]")) return;
    if (target instanceof HTMLSelectElement && target.matches("[data-submission-target-select]")) {
      const drawer = target.closest("[data-submission-drawer]");
      const option = target.selectedOptions[0];
      const requirementInput = drawer?.querySelector("[data-submission-requirement-id]");
      const dropInput = drawer?.querySelector("[data-submission-drop-id]");
      if (requirementInput instanceof HTMLInputElement) requirementInput.value = option?.dataset.requirementId ?? "";
      if (dropInput instanceof HTMLInputElement) dropInput.value = option?.dataset.dropId ?? "";
      drawerDirty = true;
      return;
    }
    drawerDirty = true;
  });
  content.addEventListener("submit", event => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement) || !form.closest("[data-submission-drawer]")) return;
    event.preventDefault();
    submitDrawerForm(form);
  });

  closeButton?.addEventListener("click", () => closeOverlay(true));
  dialog.addEventListener("click", event => {
    if (event.target === dialog && !content.querySelector("[data-submission-drawer]")) closeOverlay(true);
  });
  dialog.addEventListener("cancel", event => {
    event.preventDefault();
    const hadDrawer = Boolean(content.querySelector("[data-submission-drawer]"));
    if (!removeSubmissionDrawer()) return;
    if (!hadDrawer) closeOverlay(true);
  });
  window.addEventListener("popstate", event => {
    if (!dialog.open) return;
    if (!event.state?.teamBoardOverlay) {
      closeOverlay(false);
      return;
    }
    if (event.state.tileSidebar) openTileSidebar(window.location.href, "none");
    else restoreTeamSidebar(false);
  });
  window.addEventListener("pagehide", () => {
    ++requestGeneration;
    requestController?.abort();
    panelAnimation?.cancel();
  });

  window.addEventListener("public-progress-available", () => {
    if ((!dialog.open && !pending) || !currentTeamUrl) return;
    if (content.querySelector("[data-submission-drawer]")) return;
    saveRestoreState();
    window.location.replace(overviewUrl);
  });

  if (window.history.state?.teamBoardOverlay) window.history.replaceState(null, "", overviewUrl);

  let restoredState = null;
  try {
    restoredState = JSON.parse(window.sessionStorage.getItem(restoreKey) || "null");
    window.sessionStorage.removeItem(restoreKey);
  } catch { /* Session storage is an enhancement only. */ }
  if (restoredState?.teamUrl && restoredState?.overviewUrl &&
      new URL(restoredState.overviewUrl, window.location.href).pathname === window.location.pathname && desktop.matches) {
    const restoredPath = new URL(restoredState.teamUrl, window.location.href).pathname;
    const restoredLink = Array.from(document.querySelectorAll(".mission-team-card"))
      .find(link => new URL(link.href, window.location.href).pathname === restoredPath);
    if (restoredLink) requestAnimationFrame(() => openOverlay(restoredLink, restoredState.tileUrl));
  }
})();

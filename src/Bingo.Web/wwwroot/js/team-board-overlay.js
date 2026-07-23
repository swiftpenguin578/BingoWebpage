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
  const boardCache = new Map();

  function reducedMotion() {
    return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  }

  function overlayUrl(href) {
    const url = new URL(href, window.location.href);
    url.searchParams.set("overlay", "true");
    return url;
  }

  function publicUrl(href) {
    const url = new URL(href, window.location.href);
    url.searchParams.delete("overlay");
    return url;
  }

  function saveRestoreState() {
    if (!currentTeamUrl) return;
    try {
      window.sessionStorage.setItem(restoreKey, JSON.stringify({ overviewUrl, teamUrl: currentTeamUrl.href }));
    } catch { /* Session storage is an enhancement only. */ }
  }

  function clearRestoreState() {
    try { window.sessionStorage.removeItem(restoreKey); }
    catch { /* Session storage is an enhancement only. */ }
  }

  async function fetchWorkspace(url, signal) {
    const cachedBoard = boardCache.get(url.href);
    if (cachedBoard) return cachedBoard;

    let lastError;
    for (let attempt = 0; attempt < 2; attempt++) {
      try {
        const response = await fetch(url, {
          credentials: "same-origin",
          headers: { "X-Requested-With": "XMLHttpRequest" },
          signal
        });
        if (!response.ok) throw new Error(`Team board request failed with ${response.status}.`);

        const parsed = new DOMParser().parseFromString(await response.text(), "text/html");
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

  async function openOverlay(link) {
    if (pending || closing || dialog.open) return;

    const generation = ++requestGeneration;
    const requestedUrl = overlayUrl(link.href);
    currentTeamUrl = publicUrl(link.href);
    saveRestoreState();
    requestController = new AbortController();
    pending = true;
    window.history.pushState({ teamBoardOverlay: true }, "", publicUrl(link.href));

    try {
      const board = await fetchWorkspace(requestedUrl, requestController.signal);
      if (generation !== requestGeneration) return;

      content.replaceChildren(document.importNode(board, true));
      await prepareImages(content);
      if (generation !== requestGeneration) return;
      pending = false;
      requestController = null;
      dialog.showModal();
      document.body.classList.add("team-board-overlay-open");
      closeButton?.focus({ preventScroll: true });
    } catch (error) {
      if (generation !== requestGeneration || error?.name === "AbortError") return;
      pending = false;
      requestController = null;
      currentTeamUrl = null;
      clearRestoreState();
      window.history.replaceState(null, "", overviewUrl);
    }
  }

  function finishClose(updateHistory) {
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
    clearRestoreState();
    if (updateHistory) window.history.back();
  }

  function closeOverlay(updateHistory) {
    if (pending) {
      ++requestGeneration;
      requestController?.abort();
      requestController = null;
      pending = false;
      content.replaceChildren();
      currentTeamUrl = null;
      clearRestoreState();
      if (updateHistory) window.history.back();
      return;
    }
    if (!dialog.open || closing) return;

    closing = true;
    dialog.classList.add("closing");
    panelAnimation?.cancel();
    if (reducedMotion()) {
      finishClose(updateHistory);
      return;
    }

    panelAnimation = dialog.animate([
      { opacity: 1 },
      { opacity: 0 }
    ], { duration: 190, easing: "ease-out", fill: "forwards" });
    panelAnimation.finished.then(
      () => { if (closing) finishClose(updateHistory); },
      () => { if (closing) finishClose(updateHistory); }
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

  closeButton?.addEventListener("click", () => closeOverlay(true));
  dialog.addEventListener("click", event => { if (event.target === dialog) closeOverlay(true); });
  dialog.addEventListener("cancel", event => { event.preventDefault(); closeOverlay(true); });
  window.addEventListener("popstate", () => {
    closeOverlay(false);
  });
  window.addEventListener("pagehide", () => {
    ++requestGeneration;
    requestController?.abort();
    panelAnimation?.cancel();
  });

  window.addEventListener("public-progress-available", () => {
    if ((!dialog.open && !pending) || !currentTeamUrl) return;
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
    if (restoredLink) requestAnimationFrame(() => openOverlay(restoredLink));
  }
})();

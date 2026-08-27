(() => {
  "use strict";
  const adminText = key => document.body?.dataset[key] || "";

  let participantAddDialogCleanup = null;
  let participantEditDialogCleanup = null;
  let draftRosterPopstate = null;
  let draftRosterResize = null;

  initialize(document);
  document.addEventListener("bingo:content-updated", () => initialize(document));
  document.addEventListener("bingo:content-will-update", (event) => {
    participantAddDialogCleanup?.();
    const sourceForm = event.detail?.form;
    const isParticipantEditPost = sourceForm instanceof HTMLFormElement && sourceForm.closest("#participant-edit-dialog");
    if (!isParticipantEditPost) participantEditDialogCleanup?.();
  });
  document.addEventListener("change", (event) => {
    const control = event.target.closest("[data-auto-submit]");
    if (!(control instanceof HTMLSelectElement) && !(control instanceof HTMLInputElement && control.type === "checkbox")) return;
    control.form?.requestSubmit();
  });

  function initialize(root) {
    initializeCopyLinks(root);
    initializeDatePickers(root);
    initializeDraftInteractions(root);
    synchronizeRosterRoleDisplays(root);
    initializeOwnerAccountPicker(root);
    initializeParticipantEditDialog(root);
    root.querySelectorAll("[data-confirmation-cancel]").forEach((button) => {
      if (button.dataset.confirmationCancelReady === "true") return;
      button.dataset.confirmationCancelReady = "true";
      button.addEventListener("click", () => button.closest("details")?.removeAttribute("open"));
    });
    root.querySelectorAll(".event-participants-page").forEach((page) => {
      initializeParticipantWorkspace(page);
      initializeParticipantAddDialog(page);
    });

    root.querySelectorAll("form.participant-payment-form").forEach((form) => {
      const state = form.querySelector("[data-save-state]");
      form.addEventListener("submit", () => { if (state) state.textContent = adminText("adminSaving"); }, { once: true });
    });

    root.querySelectorAll("[data-participant-group]").forEach((group) => {
      if (group.closest(".event-participants-page")) return;
      if (group.dataset.interactionsReady === "true") return;
      group.dataset.interactionsReady = "true";
      const search = group.querySelector("[data-participant-search]");
      const tbody = group.querySelector("tbody");
      const sortButtons = [...group.querySelectorAll("[data-participant-sort]")];
      if (!tbody || !search) return;

      const noMatches = document.createElement("tr");
      noMatches.className = "participant-search-empty";
      noMatches.hidden = true;
      noMatches.innerHTML = `<td colspan="7">${group.dataset.noMatches}</td>`;
      tbody.append(noMatches);

      search?.addEventListener("input", filterRows);

      sortButtons.forEach((button) => button.addEventListener("click", () => {
        const nextDirection = button.dataset.direction === "asc" ? "desc" : "asc";
        sortButtons.forEach((item) => {
          delete item.dataset.direction;
          item.closest("th")?.removeAttribute("aria-sort");
        });
        button.dataset.direction = nextDirection;
        button.closest("th")?.setAttribute("aria-sort", nextDirection === "asc" ? "ascending" : "descending");

        const index = Number(button.dataset.sortIndex);
        const type = button.dataset.sortType;
        const multiplier = nextDirection === "asc" ? 1 : -1;
        const rows = participantRows();
        rows.sort((left, right) => compareValues(valueAt(left, index), valueAt(right, index), type) * multiplier);
        rows.forEach((row) => tbody.insertBefore(row, noMatches));
        filterRows();
      }));

      function filterRows() {
        const query = search?.value.trim().toLocaleLowerCase() ?? "";
        const rows = participantRows();
        let visible = 0;
        rows.forEach((row) => {
          const matches = !query || row.textContent.toLocaleLowerCase().includes(query);
          row.hidden = !matches;
          if (matches) visible++;
        });
        noMatches.hidden = visible > 0 || rows.length === 0;
      }

      function participantRows() {
        return [...group.querySelectorAll("[data-participant-row]")];
      }

      function valueAt(row, index) {
        const cell = row.cells[index];
        return cell?.dataset.sortValue ?? cell?.textContent.trim() ?? "";
      }

      function compareValues(left, right, type) {
        if (type === "number" || type === "date") return Number(left) - Number(right);
        return left.localeCompare(right, undefined, { numeric: true, sensitivity: "base" });
      }
    });
  }

  function initializeParticipantEditDialog(root) {
    let editor = root.querySelector("[data-participant-edit-page]");
    let participantsPage = root.querySelector(".event-participants-page");
    let triggers = participantsPage ? [...participantsPage.querySelectorAll(".participant-edit-action")] : [];
    if (!(editor instanceof HTMLElement) && triggers.length === 0) return;
    if (editor instanceof HTMLElement && editor.closest("#participant-edit-dialog")) return;

    let dialog = null;
    let content = null;
    let opener = null;
    let closing = false;
    let loadId = 0;
    let directRoute = editor instanceof HTMLElement && !participantsPage;
    let directRouteFallbackStarted = false;
    const main = document.querySelector("main#main-content");
    const pageContext = document.querySelector(".admin-page-context");
    const canEnhance = () => window.innerWidth > 900;
    const currentUrl = () => new URL(window.location.href);
    const hasOverlay = () => currentUrl().searchParams.get("overlay") === "1";
    const sourceUrl = () => opener?.getAttribute("href") || window.location.href;
    const editorRequestUrl = () => { const url = new URL(sourceUrl(), window.location.href); url.searchParams.set("overlay", "1"); return url.href; };
    const directParticipantsUrl = directRoute && editor instanceof HTMLElement && editor.dataset.participantsUrl
      ? new URL(editor.dataset.participantsUrl, window.location.href)
      : null;
    const directParticipantId = directRoute && editor instanceof HTMLElement ? editor.dataset.participantId : null;
    const standaloneEditUrl = directRoute && editor instanceof HTMLElement
      ? new URL(editor.dataset.editPath || window.location.href, window.location.href)
      : null;
    standaloneEditUrl?.searchParams.delete("overlay");
    const canonicalParticipantsUrl = () => {
      const savedUrl = history.state?.participantEditReturnUrl;
      return new URL(savedUrl || editor?.dataset.participantsUrl || window.location.href, window.location.href);
    };
    const clearParticipantDialogState = (state) => {
      const nextState = { ...(state || {}) };
      delete nextState.participantEditOverlay;
      delete nextState.participantEditBase;
      delete nextState.participantEditReturnUrl;
      return nextState;
    };

    const build = () => {
      if (dialog && document.body.contains(dialog)) return;
      dialog = null;
      content = null;
      dialog = document.createElement("dialog");
      dialog.id = "participant-edit-dialog";
      dialog.className = "admin-route-dialog participant-edit-dialog";
      content = document.createElement("div");
      content.className = "admin-route-dialog-content";
      dialog.append(content);
      (participantsPage || document.querySelector("main#main-content") || document.body).append(dialog);
      dialog.addEventListener("cancel", (event) => {
        const confirmation = content.querySelector("details.participant-confirmation-box[open]");
        if (confirmation) { event.preventDefault(); closeParticipantConfirmation(confirmation); return; }
        event.preventDefault(); closeWithHistory();
      });
      dialog.addEventListener("click", (event) => {
        if (event.target !== dialog) return;
        const confirmation = content.querySelector("details.participant-confirmation-box[open]");
        if (confirmation) { closeParticipantConfirmation(confirmation); return; }
        closeWithHistory();
      });
    };

    const parseEditor = (html) => {
      const parsed = new DOMParser().parseFromString(html, "text/html");
      const nextEditor = parsed.querySelector("[data-participant-edit-page]");
      if (!(nextEditor instanceof HTMLElement)) throw new Error("Participant editor was not returned.");
      return nextEditor;
    };

    const setEditor = (nextEditor) => {
      content.replaceChildren(document.importNode(nextEditor, true));
      bindEditor();
    };

    const replaceEditor = (html) => setEditor(parseEditor(html));

    const bindEditor = () => {
      const currentEditor = content.querySelector("[data-participant-edit-page]");
      initializeOwnerAccountPicker(content);
      const title = currentEditor?.querySelector("[data-participant-edit-close]") ? "participant-edit-dialog-title" : null;
      if (title) dialog.setAttribute("aria-labelledby", title);
      dialog.setAttribute("aria-describedby", "participant-edit-dialog-description");
      currentEditor?.querySelector("[data-participant-edit-close]")?.addEventListener("click", closeWithHistory);
      const confirmation = currentEditor?.querySelector("details.participant-confirmation-box");
      if (!(confirmation instanceof HTMLElement) || confirmation.dataset.confirmationReady === "true") return;
      confirmation.dataset.confirmationReady = "true";
      const cancel = confirmation.querySelector("[data-confirmation-cancel]");
      if (cancel instanceof HTMLElement) {
        cancel.dataset.confirmationCancelReady = "true";
        cancel.addEventListener("click", (event) => { event.preventDefault(); closeParticipantConfirmation(confirmation); });
      }
      confirmation.addEventListener("keydown", (event) => {
        if (event.key !== "Escape" || !confirmation.open) return;
        event.preventDefault();
        event.stopPropagation();
        closeParticipantConfirmation(confirmation);
      });
      confirmation.addEventListener("click", (event) => {
        if (event.target === confirmation && confirmation.open) closeParticipantConfirmation(confirmation);
      });
    };

    const closeParticipantConfirmation = (confirmation) => {
      confirmation.removeAttribute("open");
      confirmation.open = false;
      confirmation.querySelector("summary")?.focus({ preventScroll: true });
    };

    const loadDirectWorkspace = async () => {
      if (!directRoute) return;
      if (!(main instanceof HTMLElement)) throw new Error("Participants workspace main content is unavailable.");
      if (!(directParticipantsUrl instanceof URL)) throw new Error("Participants workspace URL is unavailable.");
      if (!directParticipantId) throw new Error("Participant id is unavailable.");
      const [participantsResponse, editResponse] = await Promise.all([
        window.fetch(directParticipantsUrl.href, { credentials: "same-origin" }),
        window.fetch(editorRequestUrl(), { credentials: "same-origin" })
      ]);
      if (!participantsResponse.ok) throw new Error("Participants workspace request failed.");
      if (!editResponse.ok) throw new Error("Participant editor request failed.");
      const [participantsHtml, editorHtml] = await Promise.all([participantsResponse.text(), editResponse.text()]);
      const participantsDocument = new DOMParser().parseFromString(participantsHtml, "text/html");
      const page = participantsDocument.querySelector(".event-participants-page");
      if (!(page instanceof HTMLElement)) throw new Error("Participants workspace was not returned.");
      const canonicalPageContext = participantsDocument.querySelector(".admin-page-context");
      const nextEditor = parseEditor(editorHtml);

      main.hidden = true;
      if (pageContext instanceof HTMLElement) pageContext.hidden = true;
      const currentPageContext = document.querySelector(".admin-page-context");
      if (canonicalPageContext instanceof HTMLElement && currentPageContext instanceof HTMLElement) currentPageContext.replaceWith(document.importNode(canonicalPageContext, true));
      main.replaceChildren(document.importNode(page, true));
      participantsPage = main.querySelector(".event-participants-page");
      if (!(participantsPage instanceof HTMLElement)) throw new Error("Participants workspace could not be restored.");
      const overlayHistoryState = history.state;
      initializeParticipantWorkspace(participantsPage);
      initializeParticipantAddDialog(participantsPage);
      history.replaceState(overlayHistoryState, "", currentUrl());
      opener = participantsPage.querySelector(`[data-participant-id='${directParticipantId}'] .participant-edit-action`) || participantsPage.querySelector(".participant-edit-action");
      build();
      setEditor(nextEditor);
      triggers = [...participantsPage.querySelectorAll(".participant-edit-action")];
      directRoute = false;
      editor = null;
      document.querySelector(".admin-page-context")?.removeAttribute("hidden");
      main.hidden = false;
      bindTriggers();
    };

    const prepareAndShow = async () => {
      const requestId = ++loadId;
      try {
        if (directRoute) {
          await loadDirectWorkspace();
        } else {
          build();
          const response = await window.fetch(editorRequestUrl(), { credentials: "same-origin" });
          if (!response.ok) throw new Error("Participant editor request failed.");
          replaceEditor(await response.text());
        }
        content.setAttribute("aria-busy", "true");
        if (requestId !== loadId || !hasOverlay() || !canEnhance()) return;
        if (!dialog || !document.body.contains(dialog)) throw new Error("Participant dialog is not connected.");
        if (!dialog.open) dialog.showModal();
        document.body.classList.add("admin-route-dialog-open");
        window.setTimeout(() => content.querySelector("[data-participant-edit-close], input, select, textarea")?.focus({ preventScroll: true }), 0);
      } catch {
        if (directRoute) {
          pageContext?.removeAttribute("hidden");
          document.querySelector(".admin-page-context")?.removeAttribute("hidden");
          if (main instanceof HTMLElement) main.hidden = false;
          directRoute = false;
          if (!directRouteFallbackStarted) {
            directRouteFallbackStarted = true;
            window.location.replace(standaloneEditUrl.href);
          }
          return;
        }
        pageContext?.removeAttribute("hidden");
        if (requestId === loadId && hasOverlay() && canEnhance()) {
          if (!dialog || !document.body.contains(dialog)) {
            dialog = null;
            content = null;
            build();
          }
          content.replaceChildren(Object.assign(document.createElement("p"), { textContent: adminText("adminParticipantLoadError"), role: "alert" }));
          if (!dialog.open) dialog.showModal();
          document.body.classList.add("admin-route-dialog-open");
        }
      } finally {
        if (requestId === loadId) content?.removeAttribute("aria-busy");
      }
    };

    const show = (push) => {
      if (!canEnhance()) return;
      if (push) {
        const returnUrl = currentUrl().href;
        history.pushState({ ...(history.state || {}), participantEditOverlay: true, participantEditReturnUrl: returnUrl }, "", editorRequestUrl());
      }
      prepareAndShow();
    };

    const hide = (restoreFocus = true, returnToParticipants = true) => {
      loadId++;
      if (dialog?.open) dialog.close();
      content?.replaceChildren();
      content?.removeAttribute("aria-busy");
      dialog?.remove();
      dialog = null;
      if (directRoute && returnToParticipants) window.location.replace(currentUrl().href);
      if (!document.querySelector("dialog.admin-route-dialog[open]")) document.body.classList.remove("admin-route-dialog-open");
      if (restoreFocus) window.setTimeout(() => opener instanceof HTMLElement && document.body.contains(opener) && opener.focus({ preventScroll: true }), 0);
      opener = null;
    };

    const cleanup = () => {
      if (!dialog) return;
      const url = canonicalParticipantsUrl();
      history.replaceState(clearParticipantDialogState(history.state), "", url);
      hide(false, false);
      participantEditDialogCleanup = null;
    };
    participantEditDialogCleanup = cleanup;

    function closeWithHistory() {
      if (closing) return;
      closing = true;
      if (hasOverlay() && history.state?.participantEditOverlay) history.back();
      else { const url = canonicalParticipantsUrl(); history.replaceState(clearParticipantDialogState(history.state), "", url); hide(); closing = false; }
    }

    const sync = () => {
      if (directRouteFallbackStarted) return;
      if (!canEnhance()) {
        if (hasOverlay()) {
          const url = currentUrl();
          url.searchParams.delete("overlay");
          url.pathname = editor?.dataset.editPath || url.pathname;
          window.location.replace(url.href);
          return;
        }
        if (dialog) hide(false);
        return;
      }
      if (hasOverlay()) { if (!dialog?.open) show(false); }
      else if (dialog) hide(true);
      closing = false;
    };

    const bindTriggers = () => triggers.forEach((trigger) => trigger.addEventListener("click", (event) => {
      if (!canEnhance()) return;
      event.preventDefault(); opener = trigger; show(true);
    }));
    bindTriggers();
    window.addEventListener("popstate", sync);
    window.addEventListener("resize", sync);

    if (directRoute && canEnhance()) {
      const base = canonicalParticipantsUrl();
      history.replaceState(clearParticipantDialogState(history.state), "", base);
      const reopened = new URL(window.location.href);
      reopened.pathname = editor?.dataset.editPath || reopened.pathname;
      reopened.searchParams.set("overlay", "1");
      history.pushState({ ...(history.state || {}), participantEditOverlay: true, participantEditReturnUrl: base.href }, "", reopened);
      editor.hidden = true;
      show(false);
    } else if (hasOverlay()) sync();
  }

  function initializeParticipantWorkspace(page) {
    if (page.dataset.participantInteractionsReady === "true") return;
    page.dataset.participantInteractionsReady = "true";

    const form = page.querySelector("form[data-participant-filter-form]");
    const search = form?.querySelector("[data-participant-search-input]");
    const status = form?.querySelector("select[name='ParticipantStatus']");
    const clear = form?.querySelector("[data-admin-search-clear]");
    const tables = [...page.querySelectorAll("[data-participant-table]")];
    const sortButtons = [...page.querySelectorAll("[data-participant-sort]")];
    if (!form || !search || !status) return;

    const url = new URL(window.location.href);
    let currentSort = url.searchParams.get("sort") || "ownership";
    let currentDirection = url.searchParams.get("direction") === "asc" ? "asc" : "desc";

    const updateUrl = () => {
      const nextUrl = new URL(window.location.href);
      const values = {
        ParticipantSearch: search.value.trim(),
        ParticipantStatus: status.value,
        sort: currentSort,
        direction: currentDirection
      };
      Object.entries(values).forEach(([key, value]) => value ? nextUrl.searchParams.set(key, value) : nextUrl.searchParams.delete(key));
      window.history.replaceState(null, "", `${nextUrl.pathname}${nextUrl.search}#players`);
      if (clear) clear.hidden = !values.ParticipantSearch;
    };

    const rowsFor = (table) => [...table.querySelectorAll("[data-participant-row]")];
    const sortValue = (row, key) => {
      const values = {
        sequence: [row.dataset.participantSequence, "number"],
        participant: [row.dataset.participantName, "text"],
        ehb: [row.dataset.participantEhb, "number"],
        status: [row.dataset.participantStatus, "text"],
        payment: [row.dataset.participantPayment, "text"],
        signedup: [row.dataset.participantSignedUp, "number"],
        ownership: [row.dataset.participantOwnership, "text"]
      };
      return values[key] || ["", "text"];
    };

    const sortRows = () => {
      const multiplier = currentDirection === "asc" ? 1 : -1;
      tables.forEach((table) => {
        const tbody = table.querySelector("tbody");
        const empty = table.querySelector(".participant-table-empty");
        const rows = rowsFor(table);
        const originalOrder = new Map(rows.map((row, index) => [row, index]));
        rows.sort((left, right) => {
          const [leftValue, type] = sortValue(left, currentSort);
          const [rightValue] = sortValue(right, currentSort);
          const comparison = type === "number"
            ? Number(leftValue) - Number(rightValue)
            : leftValue.localeCompare(rightValue, undefined, { numeric: true, sensitivity: "base" });
          return comparison ? comparison * multiplier : originalOrder.get(left) - originalOrder.get(right);
        });
        rows.forEach((row) => tbody?.insertBefore(row, empty || null));
      });
    };

    const updateSortHeaders = () => {
      sortButtons.forEach((button) => {
        const header = button.closest("th");
        const active = button.dataset.sortKey === currentSort;
        const icon = button.querySelector(".participant-sort-icon");
        if (!active) {
          header?.removeAttribute("aria-sort");
          icon?.remove();
          return;
        }

        header?.setAttribute("aria-sort", currentDirection === "asc" ? "ascending" : "descending");
        const activeIcon = icon ?? createSortIcon();
        if (!icon) button.append(activeIcon);
        activeIcon.querySelector("path")?.setAttribute("d", currentDirection === "asc" ? "m6 15 6-6 6 6" : "m6 9 6 6 6-6");
      });
    };

    const createSortIcon = () => {
      const icon = document.createElementNS("http://www.w3.org/2000/svg", "svg");
      icon.setAttribute("class", "participant-sort-icon");
      icon.setAttribute("aria-hidden", "true");
      icon.setAttribute("viewBox", "0 0 24 24");
      icon.setAttribute("fill", "none");
      icon.setAttribute("stroke", "currentColor");
      icon.setAttribute("stroke-linecap", "round");
      icon.setAttribute("stroke-linejoin", "round");
      icon.setAttribute("stroke-width", "2");
      const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
      icon.append(path);
      return icon;
    };

    const applyFilters = () => {
      const query = search.value.trim().toLocaleLowerCase();
      const selectedStatus = status.value.trim().toLocaleLowerCase();
      tables.forEach((table) => {
        const rows = rowsFor(table);
        let visible = 0;
        rows.forEach((row) => {
          const matches = (!query || row.textContent.toLocaleLowerCase().includes(query))
            && (!selectedStatus || row.dataset.participantStatus?.toLocaleLowerCase() === selectedStatus);
          row.hidden = !matches;
          if (matches) visible++;
        });
        const empty = table.querySelector(".participant-table-empty");
        if (empty) empty.hidden = visible > 0;
      });
      updateUrl();
    };

    search.addEventListener("input", applyFilters);
    status.addEventListener("change", applyFilters);
    clear?.addEventListener("click", (event) => { event.preventDefault(); search.value = ""; applyFilters(); search.focus(); });
    form.addEventListener("submit", () => { if (!form.action.includes("#players")) form.action = `${form.action.split("#")[0]}#players`; });
    sortButtons.forEach((button) => button.addEventListener("click", (event) => {
      event.preventDefault();
      const key = button.dataset.sortKey;
      if (!key) return;
      currentDirection = currentSort === key && currentDirection === "asc" ? "desc" : "asc";
      currentSort = key;
      sortRows();
      updateSortHeaders();
      applyFilters();
    }));

    sortRows();
    updateSortHeaders();
    applyFilters();
  }

  function initializeParticipantAddDialog(page) {
    const trigger = page.querySelector("[data-participant-add-trigger]");
    const routePage = page.querySelector(".participant-add-route-page");
    const interactionTarget = trigger instanceof HTMLElement ? trigger : routePage;
    if (!(interactionTarget instanceof HTMLElement)) return;
    if (interactionTarget.dataset.dialogInteractionsReady === "true") return;
    interactionTarget.dataset.dialogInteractionsReady = "true";

    let dialog = null;
    let content = null;
    let opener = null;
    let closing = false;
    let loadId = 0;
    const directRouteFallback = routePage instanceof HTMLElement && trigger?.hidden === true;
    const canEnhance = () => window.innerWidth > 900;
    const overlayUrl = () => new URL(window.location.href);
    const hasOverlay = () => overlayUrl().searchParams.get("addParticipant") === "1";
    const sourceUrl = () => trigger?.getAttribute("href") || window.location.href;
    const setRouteVisibility = (hidden) => { if (routePage instanceof HTMLElement) routePage.hidden = hidden; };
    const editorRequestUrl = () => {
      const url = new URL(sourceUrl(), window.location.href);
      url.searchParams.set("addParticipant", "1");
      return url.href;
    };
    const build = () => {
      if (dialog) return;
      dialog = document.createElement("dialog");
      dialog.id = "participant-add-dialog";
      dialog.className = "admin-route-dialog participant-add-dialog";
      content = document.createElement("div");
      content.className = "admin-route-dialog-content";
      dialog.append(content);
      page.append(dialog);
      dialog.addEventListener("cancel", (event) => { event.preventDefault(); closeWithHistory(); });
      dialog.addEventListener("click", (event) => { if (event.target === dialog) closeWithHistory(); });
    };

    const replaceEditor = (html) => {
      const parsed = new DOMParser().parseFromString(html, "text/html");
      const editor = parsed.querySelector(".participant-add-dialog-page");
      if (!(editor instanceof HTMLElement)) throw new Error("Participant editor was not returned.");
      content.replaceChildren(document.importNode(editor, true));
      const currentEditor = content.querySelector(".participant-add-dialog-page");
      const title = currentEditor?.getAttribute("aria-labelledby");
      const description = currentEditor?.getAttribute("aria-describedby");
      if (title) dialog.setAttribute("aria-labelledby", title);
      else dialog.removeAttribute("aria-labelledby");
      if (description) dialog.setAttribute("aria-describedby", description);
      else dialog.removeAttribute("aria-describedby");
      currentEditor?.querySelector("[data-participant-add-close]")?.addEventListener("click", closeWithHistory);
      if (currentEditor) initializeOwnerAccountPicker(currentEditor);
    };

    const prepareAndShow = async () => {
      build();
      const requestId = ++loadId;
      content.setAttribute("aria-busy", "true");
      try {
        const response = await window.fetch(editorRequestUrl(), { credentials: "same-origin" });
        if (!response.ok) throw new Error("Participant editor request failed.");
        const html = await response.text();
        if (requestId !== loadId || !hasOverlay() || !canEnhance()) return;
        replaceEditor(html);
        if (!dialog.open) dialog.showModal();
        document.body.classList.add("admin-route-dialog-open");
        window.setTimeout(() => content.querySelector("[data-participant-add-close], input, select, textarea")?.focus({ preventScroll: true }), 0);
      } catch {
        if (requestId === loadId && hasOverlay() && canEnhance()) {
          content.replaceChildren(Object.assign(document.createElement("p"), { textContent: adminText("adminParticipantFormError"), role: "alert" }));
          if (!dialog.open) dialog.showModal();
          document.body.classList.add("admin-route-dialog-open");
        }
      } finally {
        if (requestId === loadId) content.removeAttribute("aria-busy");
      }
    };

    const show = (push) => {
      if (!canEnhance()) return;
      if (push) {
        const url = overlayUrl();
        url.searchParams.set("addParticipant", "1");
        history.pushState({ ...(history.state || {}), participantAddOverlay: true }, "", url);
      }
      prepareAndShow();
    };

    const hide = (restoreFocus = true, returnToParticipants = true) => {
      loadId++;
      if (directRouteFallback && returnToParticipants) {
        routePage.hidden = true;
        trigger.hidden = false;
      } else if (!directRouteFallback || returnToParticipants) {
        setRouteVisibility(false);
      }
      if (dialog?.open) dialog.close();
      content?.replaceChildren();
      content?.removeAttribute("aria-busy");
      dialog?.remove();
      dialog = null;
      if (!document.querySelector("dialog.admin-route-dialog[open]")) document.body.classList.remove("admin-route-dialog-open");
      const focusTarget = directRouteFallback && returnToParticipants ? trigger : opener;
      if (restoreFocus) window.setTimeout(() => {
        if (document.body.contains(focusTarget) && !focusTarget.hidden) focusTarget.focus({ preventScroll: true });
      }, 0);
      opener = null;
    };

    const cleanupBeforeUpdate = () => {
      if (!dialog) return;
      const url = overlayUrl();
      url.searchParams.delete("addParticipant");
      history.replaceState(history.state, "", url);
      hide(false, false);
      participantAddDialogCleanup = null;
    };
    participantAddDialogCleanup = cleanupBeforeUpdate;

    function closeWithHistory() {
      if (closing) return;
      closing = true;
      if (hasOverlay() && history.state?.participantAddOverlay) history.back();
      else {
        const url = overlayUrl();
        url.searchParams.delete("addParticipant");
        history.replaceState(history.state, "", url);
        hide();
        closing = false;
      }
    }

    const sync = () => {
      if (!canEnhance()) {
        setRouteVisibility(false);
        if (hasOverlay()) {
          const url = overlayUrl();
          url.searchParams.delete("addParticipant");
          history.replaceState(history.state, "", url);
        }
        if (dialog) hide(false, false);
        return;
      }
      if (hasOverlay()) {
        setRouteVisibility(true);
        if (!dialog?.open) show(false);
      } else {
        setRouteVisibility(false);
        if (dialog) hide();
      }
      closing = false;
    };

    trigger?.addEventListener("click", (event) => {
      if (!canEnhance()) return;
      event.preventDefault();
      opener = trigger;
      show(true);
    });
    window.addEventListener("popstate", sync);
    window.addEventListener("resize", sync);

    if (hasOverlay() && canEnhance()) {
      setRouteVisibility(true);
      const base = overlayUrl();
      base.searchParams.delete("addParticipant");
      history.replaceState({ ...(history.state || {}), participantAddBase: true }, "", base);
      const reopened = new URL(window.location.href);
      reopened.searchParams.set("addParticipant", "1");
      history.pushState({ ...(history.state || {}), participantAddOverlay: true }, "", reopened);
      show(false);
    } else if (hasOverlay()) sync();
  }

  function initializeOwnerAccountPicker(root) {
    const pickers = [];
    if (root instanceof HTMLElement && root.matches("[data-owner-account-picker]")) pickers.push(root);
    pickers.push(...root.querySelectorAll("[data-owner-account-picker]"));
    pickers.forEach((picker) => {
      if (picker.dataset.ownerAccountPickerReady === "true") return;
      picker.dataset.ownerAccountPickerReady = "true";
      const search = picker.querySelector("[data-owner-account-search]");
      const ownerId = picker.querySelector("[data-owner-account-id]");
      const results = picker.querySelector("[data-owner-account-results]");
      if (!(search instanceof HTMLInputElement) || !(ownerId instanceof HTMLInputElement) || !(results instanceof HTMLElement)) return;

      let controller = null;
      const clearResults = () => {
        results.replaceChildren();
        results.hidden = true;
        search.setAttribute("aria-expanded", "false");
      };
      const showResults = (accounts) => {
        results.replaceChildren();
        accounts.forEach((account) => {
          const option = document.createElement("button");
          option.type = "button";
          option.role = "option";
          option.className = "admin-button-secondary";
          option.textContent = account.username;
          option.addEventListener("click", () => {
            ownerId.value = account.id;
            search.value = account.username;
            clearResults();
          });
          results.append(option);
        });
        results.hidden = accounts.length === 0;
        search.setAttribute("aria-expanded", accounts.length > 0 ? "true" : "false");
      };
      const searchAccounts = async () => {
        ownerId.value = "";
        const query = search.value.trim();
        if (!query) {
          clearResults();
          return;
        }
        controller?.abort();
        const requestController = new AbortController();
        controller = requestController;
        const url = new URL(picker.dataset.ownerAccountSearchUrl || window.location.href, window.location.href);
        url.searchParams.set("handler", "SearchOwnerAccounts");
        url.searchParams.set("search", query);
        try {
          const response = await window.fetch(url, { credentials: "same-origin", signal: requestController.signal });
          if (controller !== requestController) return;
          if (!response.ok) {
            clearResults();
            return;
          }
          const accounts = await response.json();
          if (controller !== requestController || !Array.isArray(accounts)) return;
          showResults(accounts);
        } catch (error) {
          if (controller === requestController && error.name !== "AbortError") clearResults();
        }
      };

      search.addEventListener("input", searchAccounts);
    });
  }

  function initializeCopyLinks(root) {
    root.querySelectorAll("[data-copy-url]").forEach((button) => {
      if (button.dataset.copyReady === "true") return;
      button.dataset.copyReady = "true";
      button.addEventListener("click", async () => {
        const url = button.dataset.copyUrl;
        if (!url) return;
        try {
          await navigator.clipboard.writeText(url);
          window.showBingoToast?.(adminText("adminCopySuccess"));
        } catch {
          window.showBingoToast?.(adminText("adminCopyError"), "error");
        }
      });
    });
  }

  function synchronizeRosterRoleDisplays(root) {
    root.querySelectorAll("form[data-role-display]").forEach((form) => {
      const select = form.querySelector("select[name='role']");
      const display = document.querySelector(form.dataset.roleDisplay);
      if (!(select instanceof HTMLSelectElement) || !display) return;
      display.textContent = select.selectedOptions[0]?.textContent?.trim() || select.value;
    });
  }

  function initializeDatePickers(root) {
    if (typeof window.flatpickr !== "function") return;
    root.querySelectorAll("[data-event-manage-datetime-picker]").forEach((input) => {
      if (input._flatpickr) return;
      window.initializeBingoDateTimePicker(input, {
        dateFormat: "Z",
        altInput: true,
        altFormat: "d/m/Y H:i",
        defaultDate: input.value,
        timeLabel: input.dataset.timeLabel || adminText("adminTime")
      });
    });
  }

  function initializeDraftInteractions(root) {
    root.querySelectorAll(".draft-page [data-draft-role-form]").forEach((form) => {
      if (form.dataset.draftRoleReady === "true") return;
      form.dataset.draftRoleReady = "true";
      form.classList.add("draft-role-form-enhanced");
    });

    root.querySelectorAll(".draft-page .draft-confirmation").forEach((confirmation) => {
      if (confirmation.dataset.draftConfirmationReady === "true") return;
      confirmation.dataset.draftConfirmationReady = "true";
      const close = () => {
        confirmation.removeAttribute("open");
        confirmation.querySelector(":scope > summary")?.focus({ preventScroll: true });
      };
      confirmation.querySelector("[data-confirmation-cancel]")?.addEventListener("click", (event) => { event.preventDefault(); close(); });
      confirmation.addEventListener("keydown", (event) => {
        if (event.key !== "Escape" || !confirmation.open) return;
        event.preventDefault();
        event.stopPropagation();
        close();
      });
    });

    const rosterDialogs = [...root.querySelectorAll(".draft-page dialog.team-roster-dialog")]
      .filter((dialog) => dialog.querySelector("[data-draft-roster-close]"));
    const rosterDialogForRoute = () => {
      const teamId = new URL(window.location.href).searchParams.get("rosterTeamId");
      return teamId
        ? rosterDialogs.find((dialog) => dialog.id.toLowerCase() === `team-${teamId}`.toLowerCase())
        : null;
    };
    const closeRosterDialog = (dialog, { fromPopstate = false, restoreFocus = true } = {}) => {
      const opener = dialog._draftOpener;
      if (dialog.open) dialog.close();
      dialog.removeAttribute("open");
      document.body.classList.remove("admin-route-dialog-open");
      dialog._draftRosterModal = false;
      dialog._draftOpener = null;
      if (restoreFocus) window.setTimeout(() => opener?.focus?.({ preventScroll: true }), 0);

      const url = new URL(window.location.href);
      if (fromPopstate || !url.searchParams.has("rosterTeamId")) return;
      if (history.state?.draftRosterDialog) {
        history.back();
        return;
      }
      url.searchParams.delete("rosterTeamId");
      history.replaceState(history.state, "", url);
    };
    const openRosterDialog = (dialog) => {
      if (window.innerWidth <= 900) return;
      let isModal = dialog._draftRosterModal === true;
      try { isModal ||= dialog.matches(":modal"); } catch { /* Older browsers do not expose :modal. */ }
      if (isModal) {
        document.body.classList.add("admin-route-dialog-open");
        return;
      }
      if (dialog.open) dialog.close();
      dialog.removeAttribute("open");
      dialog.showModal();
      dialog._draftRosterModal = true;
      document.body.classList.add("admin-route-dialog-open");
      window.setTimeout(() => dialog.querySelector("[data-draft-roster-close]")?.focus({ preventScroll: true }), 0);
    };
    const synchronizeRosterDialog = () => {
      if (window.innerWidth <= 900) return;
      const routeDialog = rosterDialogForRoute();
      rosterDialogs.filter((dialog) => dialog !== routeDialog && (dialog.open || dialog.hasAttribute("open")))
        .forEach((dialog) => closeRosterDialog(dialog, { fromPopstate: true, restoreFocus: false }));
      if (routeDialog instanceof HTMLDialogElement) openRosterDialog(routeDialog);
      else document.body.classList.remove("admin-route-dialog-open");
    };

    rosterDialogs.forEach((dialog) => {
      if (dialog.dataset.draftRosterDialogReady === "true") return;
      dialog.dataset.draftRosterDialogReady = "true";
      dialog.querySelector("[data-draft-roster-close]")?.addEventListener("click", (event) => {
        event.preventDefault();
        closeRosterDialog(dialog);
      });
      dialog.addEventListener("cancel", (event) => {
        event.preventDefault();
        closeRosterDialog(dialog);
      });
    });
    root.querySelectorAll(".draft-page [data-draft-roster-trigger]").forEach((trigger) => {
      if (trigger.dataset.draftRosterReady === "true") return;
      trigger.dataset.draftRosterReady = "true";
      trigger.addEventListener("click", (event) => {
        if (window.innerWidth <= 900) return;
        const dialog = document.getElementById(trigger.dataset.draftRosterTrigger || "");
        if (!(dialog instanceof HTMLDialogElement)) return;
        event.preventDefault();
        dialog._draftOpener = trigger;
        const url = new URL(trigger.href, window.location.href);
        if (url.href !== window.location.href) {
          history.pushState({ ...(history.state || {}), draftRosterDialog: true }, "", url);
        }
        synchronizeRosterDialog();
      });
    });
    draftRosterPopstate && window.removeEventListener("popstate", draftRosterPopstate);
    draftRosterResize && window.removeEventListener("resize", draftRosterResize);
    draftRosterPopstate = synchronizeRosterDialog;
    draftRosterResize = synchronizeRosterDialog;
    window.addEventListener("popstate", draftRosterPopstate);
    window.addEventListener("resize", draftRosterResize);
    synchronizeRosterDialog();

    root.querySelectorAll(".draft-page [data-draft-dialog-close]").forEach((button) => {
      if (button.dataset.draftDialogCloseReady === "true") return;
      button.dataset.draftDialogCloseReady = "true";
      button.addEventListener("click", () => {
        const dialog = button.closest("dialog");
        if (!(dialog instanceof HTMLDialogElement)) return;
        dialog.close();
        dialog._draftOpener?.focus?.({ preventScroll: true });
        dialog._draftOpener = null;
      });
    });

    root.querySelectorAll(".draft-page [data-draft-dialog-open]").forEach((trigger) => {
      if (trigger.dataset.draftDialogOpenReady === "true") return;
      trigger.dataset.draftDialogOpenReady = "true";
      trigger.addEventListener("click", () => {
        const dialog = document.getElementById(trigger.dataset.draftDialogOpen || "");
        if (dialog instanceof HTMLDialogElement) dialog._draftOpener = trigger;
      });
    });
  }
})();

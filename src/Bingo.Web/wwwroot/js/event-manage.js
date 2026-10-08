(() => {
  "use strict";
  const adminText = key => document.body?.dataset[key] || "";

  let participantAddDialogCleanup = null;
  let participantEditDialogCleanup = null;

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
    initializeConfirmationFocus(root);
    initializeWomValidationConfirmation(root);
    initializeOverviewLifecycleConfirmations(root);
    initializeCopyLinks(root);
    initializeDatePickers(root);
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

  function initializeWomValidationConfirmation(root) {
    root.querySelectorAll("[data-wom-validation-confirmation]").forEach((confirmation) => {
      if (confirmation.dataset.womValidationConfirmationReady === "true") return;
      confirmation.dataset.womValidationConfirmationReady = "true";
      const cancel = confirmation.querySelector("[data-wom-validation-cancel]");
      if (!(cancel instanceof HTMLElement)) return;
      confirmation.addEventListener("keydown", event => {
        if (event.key !== "Escape") return;
        event.preventDefault();
        event.stopPropagation();
        cancel.click();
      });
    });
  }

  function initializeConfirmationFocus(root) {
    root.querySelectorAll("[data-confirmation-box]").forEach((confirmation) => {
      if (!(confirmation instanceof HTMLElement) || confirmation.dataset.confirmationFocusReady === "true") return;
      confirmation.dataset.confirmationFocusReady = "true";
      requestAnimationFrame(() => requestAnimationFrame(() => requestAnimationFrame(() => {
        if (!confirmation.isConnected) return;
        confirmation.focus({ preventScroll: true });
        confirmation.scrollIntoView({ block: "nearest" });
      })));
    });
  }

  function initializeOverviewLifecycleConfirmations(root) {
    const page = root.matches?.(".event-manage-page") ? root : root.querySelector(".event-manage-page");
    if (!(page instanceof HTMLElement)) return;

    page.querySelectorAll("[data-confirmation-box]").forEach((confirmation) => {
      if (!(confirmation instanceof HTMLElement) || confirmation.dataset.overviewLifecycleReady === "true") return;
      confirmation.dataset.overviewLifecycleReady = "true";

      const isPending = () => [...confirmation.querySelectorAll("form")]
        .some(form => form.dataset.historySubmitting === "true");

      confirmation.addEventListener("click", (event) => {
        if (!isPending()) return;
        const control = event.target instanceof Element ? event.target.closest("a, button") : null;
        if (!(control instanceof HTMLElement) || !confirmation.contains(control)) return;
        event.preventDefault();
        event.stopPropagation();
      }, true);

      confirmation.addEventListener("keydown", (event) => {
        if (event.key !== "Escape") return;
        if (isPending()) {
          event.preventDefault();
          event.stopPropagation();
          return;
        }

        const cancel = confirmation.querySelector(".event-confirmation-actions a[href]");
        if (!(cancel instanceof HTMLElement)) return;
        event.preventDefault();
        event.stopPropagation();
        cancel.click();
      }, true);
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
    let restoringHistory = false;
    let parentRefreshFailed = false;
    const currentEditor = () => content?.querySelector("[data-participant-edit-page]") || editor;
    const closeOpenConfirmation = () => {
      const confirmation = currentEditor()?.querySelector("details.participant-confirmation-box[open]");
      if (!confirmation) return false;
      closeParticipantConfirmation(confirmation);
      return true;
    };
    const guard = window.createAdminEditorGuard({ editor: currentEditor, prefix: "participant-editor", closeConfirmation: closeOpenConfirmation, saveError: () => adminText("signupQuestionsSaveError") });
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
        event.preventDefault();
        if (!guard.pending && !guard.cancelDiscard() && !closeOpenConfirmation()) closeWithHistory();
      });
      dialog.addEventListener("click", (event) => {
        if (event.target !== dialog) return;
        if (!guard.pending && !guard.cancelDiscard() && !closeOpenConfirmation()) closeWithHistory();
      });
    };

    const parseEditor = (html) => {
      const parsed = new DOMParser().parseFromString(html, "text/html");
      const nextEditor = parsed.querySelector("[data-participant-edit-page]");
      if (!(nextEditor instanceof HTMLElement)) throw new Error("Participant editor was not returned.");
      return nextEditor;
    };

    const setEditor = (nextEditor) => {
      const replacement = document.importNode(nextEditor, true);
      if (content) content.replaceChildren(replacement);
      else { editor.replaceWith(replacement); editor = replacement; }
      bindEditor();
    };

    const replaceEditor = (html) => setEditor(parseEditor(html));

    const bindEditor = () => {
      const currentEditor = content?.querySelector("[data-participant-edit-page]") || editor;
      initializeOwnerAccountPicker(currentEditor);
      initializeWomValidationConfirmation(currentEditor);
      const title = currentEditor?.querySelector("[data-participant-edit-close]") ? "participant-edit-dialog-title" : null;
      if (title) dialog?.setAttribute("aria-labelledby", title);
      dialog?.setAttribute("aria-describedby", "participant-edit-dialog-description");
      currentEditor?.querySelector("[data-participant-edit-close]")?.addEventListener("click", closeWithHistory);
      guard.initialize();
      currentEditor?.querySelectorAll("form").forEach(form => {
        form.addEventListener("submit", event => {
          if (event.defaultPrevented) return;
          event.preventDefault();
          if (guard.pending) return;
          const submit = () => submitParticipantForm(form, event.submitter);
          if (guard.dirtyForms(form).length) guard.confirmDiscard(submit);
          else submit();
        });
      });
      currentEditor?.addEventListener("click", event => {
        if (guard.pending) { event.preventDefault(); event.stopPropagation(); }
      }, true);
      currentEditor?.querySelectorAll("details.participant-confirmation-box").forEach(confirmation => {
        if (confirmation.dataset.confirmationReady === "true") return;
        confirmation.dataset.confirmationReady = "true";
        confirmation.addEventListener("toggle", () => {
          if (!confirmation.open) return;
          currentEditor.querySelectorAll("details.participant-confirmation-box[open]").forEach(other => { if (other !== confirmation) other.open = false; });
          confirmation.scrollIntoView?.({ block: "nearest" });
          confirmation.querySelector("[data-confirmation-cancel]")?.focus({ preventScroll: true });
        });
        const cancel = confirmation.querySelector("[data-confirmation-cancel]");
        if (cancel instanceof HTMLElement) {
          cancel.dataset.confirmationCancelReady = "true";
          cancel.addEventListener("click", (event) => { event.preventDefault(); closeParticipantConfirmation(confirmation); });
        }
        confirmation.addEventListener("keydown", (event) => {
          if (event.key !== "Escape" || !confirmation.open) return;
          if (guard.pending) { event.preventDefault(); event.stopPropagation(); return; }
          event.preventDefault();
          event.stopPropagation();
          closeParticipantConfirmation(confirmation);
        });
      });
    };

    const refreshParticipants = async () => {
      if (!participantsPage) return;
      parentRefreshFailed = true;
      const response = await window.fetch(canonicalParticipantsUrl().href, { credentials: "same-origin" });
      if (!response.ok) throw new Error("Participants refresh failed.");
      const page = new DOMParser().parseFromString(await response.text(), "text/html").querySelector(".event-participants-page");
      if (!(page instanceof HTMLElement)) throw new Error("Participants workspace was not returned.");
      const scroll = { left: window.scrollX, top: window.scrollY };
      const overlayUrl = currentUrl();
      const overlayState = history.state;
      const openerHref = opener?.getAttribute("href");
      participantAddDialogCleanup?.();
      [...participantsPage.children].filter(child => child !== dialog).forEach(child => child.remove());
      participantsPage.prepend(...Array.from(page.children, child => document.importNode(child, true)));
      delete participantsPage.dataset.participantInteractionsReady;
      history.replaceState(overlayState, "", canonicalParticipantsUrl());
      initializeParticipantWorkspace(participantsPage);
      initializeParticipantAddDialog(participantsPage);
      history.replaceState(overlayState, "", overlayUrl);
      triggers = [...participantsPage.querySelectorAll(".participant-edit-action")];
      opener = triggers.find(trigger => trigger.getAttribute("href") === openerHref) || participantsPage.querySelector("[data-participant-search-input]");
      bindTriggers();
      document.dispatchEvent(new CustomEvent("bingo:content-updated", { detail: { selectors: [".event-participants-page"] } }));
      window.scrollTo(scroll);
      parentRefreshFailed = false;
    };

    const submitParticipantForm = async (form, submitter) => {
      if (guard.pending) return;
      const body = new FormData(form);
      if (submitter?.name) body.append(submitter.name, submitter.value);
      const finish = guard.begin(form);
      let finished = false;
      const navigateAfterSave = destination => {
        finish();
        finished = true;
        guard.initialize();
        window.location.assign(destination);
      };
      try {
        const response = await window.fetch(submitter?.getAttribute("formaction") || form.action, {
          method: "POST", body, credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        if (!response.ok) throw new Error();
        const navigation = response.headers.get("X-Bingo-Post-Navigation");
        if (navigation) { navigateAfterSave(navigation); return; }
        const html = await response.text();
        const parsed = new DOMParser().parseFromString(html, "text/html");
        const nextEditor = parsed.querySelector("[data-participant-edit-page]");
        if (nextEditor?.querySelector("[data-wom-validation-confirmation]")) {
          const scrollTop = content?.scrollTop;
          setEditor(nextEditor);
          if (content) content.scrollTop = scrollTop;
          currentEditor()?.querySelector("[data-wom-validation-cancel]")?.focus({ preventScroll: true });
          return;
        }
        const errors = Array.from(parsed.querySelectorAll(".field-validation-error, .validation-summary-errors, .app-toast-error .app-toast-copy span, .app-toast-warning .app-toast-copy span, [data-toast-host] [data-toast].is-error [data-component-text]")).map(error => error.textContent.trim()).filter(Boolean);
        if (errors.length) { guard.showFailure(errors.join(" ")); return; }
        if (!(nextEditor instanceof HTMLElement)) {
          if (response.redirected && new URL(response.url).pathname !== currentUrl().pathname) { navigateAfterSave(response.url); return; }
          throw new Error();
        }
        const scrollTop = content?.scrollTop;
        setEditor(nextEditor);
        if (content) content.scrollTop = scrollTop;
        const notice = parsed.querySelector("#app-notice-region");
        const noticeRegion = document.querySelector("#app-notice-region");
        if (notice && noticeRegion) {
          noticeRegion.replaceChildren(...Array.from(notice.childNodes, node => document.importNode(node, true)));
          window.initializeTransientToastLayer?.();
        }
        try { await refreshParticipants(); }
        catch { guard.showFailure(document.body.dataset.adminParentRefreshError); return; }
        currentEditor()?.querySelector("[data-participant-edit-close]")?.focus({ preventScroll: true });
      } catch {
        guard.showFailure();
      } finally { if (!finished) finish(); }
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
      document.dispatchEvent(new CustomEvent("bingo:content-updated", { detail: { selectors: [".event-participants-page"] } }));
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
          const html = await response.text();
          if (requestId !== loadId || !hasOverlay()) return;
          replaceEditor(html);
        }
        content.setAttribute("aria-busy", "true");
        if (requestId !== loadId || !hasOverlay()) return;
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
        if (requestId === loadId && hasOverlay()) {
          if (!dialog || !document.body.contains(dialog)) {
            dialog = null;
            content = null;
            build();
          }
          const message = Object.assign(document.createElement("p"), { textContent: adminText("adminParticipantLoadError"), role: "alert" });
          const retry = Object.assign(document.createElement("button"), { type: "button", className: "admin-button-secondary", textContent: adminText("signupQuestionsRetry") });
          const close = Object.assign(document.createElement("button"), { type: "button", className: "admin-button-secondary", textContent: adminText("adminParticipantClose") });
          retry.addEventListener("click", prepareAndShow);
          close.addEventListener("click", closeWithHistory);
          content.replaceChildren(message, retry, close);
          if (!dialog.open) dialog.showModal();
          document.body.classList.add("admin-route-dialog-open");
        }
      } finally {
        if (requestId === loadId) content?.removeAttribute("aria-busy");
      }
    };

    const show = (push) => {
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
      if ((directRoute || parentRefreshFailed) && returnToParticipants) {
        const url = canonicalParticipantsUrl();
        if (url.href === currentUrl().href) window.location.reload();
        else window.location.replace(url.href);
      }
      if (!document.querySelector("dialog.admin-route-dialog[open]")) document.body.classList.remove("admin-route-dialog-open");
      if (restoreFocus) window.setTimeout(() => opener instanceof HTMLElement && document.body.contains(opener) && opener.focus({ preventScroll: true }), 0);
      opener = null;
    };

    const cleanup = () => {
      window.removeEventListener("popstate", sync);
      stopWatchingUnsaved();
      if (!dialog) return;
      const url = canonicalParticipantsUrl();
      history.replaceState(clearParticipantDialogState(history.state), "", url);
      hide(false, false);
      participantEditDialogCleanup = null;
    };
    participantEditDialogCleanup = cleanup;

    function closeWithHistory() {
      if (closing || guard.pending) return;
      if (guard.dirtyForms().length) { guard.confirmDiscard(closeNow); return; }
      closeNow();
    }

    function closeNow() {
      if (closing || guard.pending) return;
      closing = true;
      if (hasOverlay() && history.state?.participantEditOverlay) history.back();
      else { const url = canonicalParticipantsUrl(); history.replaceState(clearParticipantDialogState(history.state), "", url); hide(); closing = false; }
    }

    const sync = () => {
      if (directRouteFallbackStarted) return;
      if (restoringHistory && hasOverlay()) {
        restoringHistory = false;
        if (!guard.pending && guard.dirtyForms().length) guard.confirmDiscard(closeNow);
        return;
      }
      if (!hasOverlay() && dialog?.open && !closing && (guard.pending || guard.dirtyForms().length)) {
        restoringHistory = true;
        history.forward();
        return;
      }
      if (hasOverlay()) { if (!dialog?.open) show(false); }
      else if (dialog) hide(true);
      closing = false;
    };

    const bindTriggers = () => triggers.forEach((trigger) => trigger.addEventListener("click", (event) => {
      event.preventDefault(); opener = trigger; show(true);
    }));
    bindTriggers();
    window.addEventListener("popstate", sync);
    const stopWatchingUnsaved = window.watchAdminUnsavedChanges(() => guard.dirtyForms().length > 0);

    if (directRoute && !hasOverlay()) { bindEditor(); return; }
    if (directRoute) {
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
        captain: [row.dataset.participantCaptain, "number"],
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
    let parentRefreshFailed = false;
    let completedWithoutEditor = false;
    const directRouteFallback = routePage instanceof HTMLElement && trigger?.hidden === true;
    let restoringHistory = false;
    const currentUrl = () => new URL(window.location.href);
    const overlayUrl = () => new URL(window.location.href);
    const hasOverlay = () => overlayUrl().searchParams.get("addParticipant") === "1";
    const setRouteVisibility = (hidden) => { if (routePage instanceof HTMLElement) routePage.hidden = hidden; };
    const editorRequestUrl = () => {
      const url = currentUrl();
      url.searchParams.set("addParticipant", "1");
      url.hash = "";
      return url.href;
    };
    const currentEditor = () => content?.querySelector(".participant-add-dialog-page") || routePage?.querySelector(".participant-add-dialog-page") || null;
    const guard = window.createAdminEditorGuard({
      editor: currentEditor,
      prefix: "participant-add",
      saveError: () => adminText("adminPostError")
    });
    const build = () => {
      if (dialog && document.body.contains(dialog)) return;
      dialog = document.createElement("dialog");
      dialog.id = "participant-add-dialog";
      dialog.className = "admin-route-dialog participant-add-dialog";
      content = document.createElement("div");
      content.className = "admin-route-dialog-content";
      dialog.append(content);
      page.append(dialog);
      dialog.addEventListener("cancel", (event) => {
        event.preventDefault();
        if (!guard.pending && !guard.cancelDiscard()) closeWithHistory();
      });
      dialog.addEventListener("click", (event) => {
        if (event.target !== dialog || guard.pending || guard.cancelDiscard()) return;
        closeWithHistory();
      });
    };

    const replaceEditor = (html) => {
      const parsed = new DOMParser().parseFromString(html, "text/html");
      const editor = parsed.querySelector(".participant-add-dialog-page");
      if (!(editor instanceof HTMLElement)) throw new Error("Participant editor was not returned.");
      content.replaceChildren(document.importNode(editor, true));
      bindEditor();
    };

    const openDialog = () => {
      if (!dialog.open) dialog.showModal();
      document.body.classList.add("admin-route-dialog-open");
      window.setTimeout(() => content.querySelector("[data-participant-add-close], input, select, textarea")?.focus({ preventScroll: true }), 0);
    };

    const moveRouteEditor = () => {
      const editor = routePage?.querySelector(".participant-add-dialog-page");
      if (!(editor instanceof HTMLElement)) throw new Error("Participant editor was not returned.");
      content.replaceChildren(editor);
      bindEditor();
    };

    const bindEditor = () => {
      const currentEditor = content.querySelector(".participant-add-dialog-page");
      if (!(currentEditor instanceof HTMLElement)) return;
      const title = currentEditor?.getAttribute("aria-labelledby");
      const description = currentEditor?.getAttribute("aria-describedby");
      if (title) dialog.setAttribute("aria-labelledby", title);
      else dialog.removeAttribute("aria-labelledby");
      if (description) dialog.setAttribute("aria-describedby", description);
      else dialog.removeAttribute("aria-describedby");
      currentEditor?.querySelector("[data-participant-add-close]")?.addEventListener("click", closeWithHistory);
      initializeOwnerAccountPicker(currentEditor);
      initializeWomValidationConfirmation(currentEditor);
      guard.initialize();
      currentEditor.querySelectorAll("form").forEach(form => {
        form.addEventListener("submit", event => {
          if (event.defaultPrevented) return;
          event.preventDefault();
          if (guard.pending || completedWithoutEditor) return;
          const submit = () => submitParticipantAddForm(form, event.submitter);
          if (guard.dirtyForms(form).length) guard.confirmDiscard(submit);
          else submit();
        });
      });
      currentEditor.addEventListener("click", event => {
        if (guard.pending) { event.preventDefault(); event.stopPropagation(); }
      }, true);
    };

    const prepareAndShow = async () => {
      build();
      const requestId = ++loadId;
      content.setAttribute("aria-busy", "true");
      try {
        if (directRouteFallback && routePage?.querySelector(".participant-add-dialog-page")) {
          moveRouteEditor();
        } else {
          const response = await window.fetch(editorRequestUrl(), { credentials: "same-origin" });
          if (!response.ok) throw new Error("Participant editor request failed.");
          const html = await response.text();
          if (requestId !== loadId || !hasOverlay()) return;
          replaceEditor(html);
        }
        if (requestId !== loadId || !hasOverlay()) return;
        openDialog();
      } catch {
        if (requestId === loadId && hasOverlay()) {
          const message = Object.assign(document.createElement("p"), { textContent: adminText("adminParticipantFormError"), role: "alert" });
          const retry = Object.assign(document.createElement("button"), { type: "button", className: "admin-button-secondary", textContent: adminText("signupQuestionsRetry") });
          const close = Object.assign(document.createElement("button"), { type: "button", className: "admin-button-secondary", textContent: adminText("adminParticipantAddClose") });
          retry.addEventListener("click", prepareAndShow);
          close.addEventListener("click", closeWithHistory);
          content.replaceChildren(message, retry, close);
          openDialog();
        }
      } finally {
        if (requestId === loadId) content.removeAttribute("aria-busy");
      }
    };

    const show = (push) => {
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
      if (parentRefreshFailed && returnToParticipants) {
        const url = overlayUrl();
        url.searchParams.delete("addParticipant");
        if (url.href === currentUrl().href) window.location.reload();
        else window.location.replace(url.href);
      }
      const focusTarget = directRouteFallback && returnToParticipants ? trigger : opener;
      if (restoreFocus) window.setTimeout(() => {
        if (document.body.contains(focusTarget) && !focusTarget.hidden) focusTarget.focus({ preventScroll: true });
      }, 0);
      opener = null;
    };

    const stopWatchingUnsaved = window.watchAdminUnsavedChanges(() => !completedWithoutEditor && guard.dirtyForms().length > 0);

    const cleanupBeforeUpdate = () => {
      window.removeEventListener("popstate", sync);
      stopWatchingUnsaved();
      participantAddDialogCleanup = null;
      const url = overlayUrl();
      url.searchParams.delete("addParticipant");
      history.replaceState(history.state, "", url);
      if (dialog) hide(false, false);
    };

    const dispose = () => {
      window.removeEventListener("popstate", sync);
      stopWatchingUnsaved();
      participantAddDialogCleanup = null;
      if (dialog?.open) dialog.close();
      content?.replaceChildren();
      content?.removeAttribute("aria-busy");
      dialog?.remove();
      dialog = null;
      content = null;
      setRouteVisibility(false);
      if (!document.querySelector("dialog.admin-route-dialog[open]")) document.body.classList.remove("admin-route-dialog-open");
      opener = null;
    };

    function closeWithHistory() {
      if (closing || guard.pending) return;
      if (!completedWithoutEditor && guard.dirtyForms().length) { guard.confirmDiscard(closeNow); return; }
      closeNow();
    }

    function closeNow() {
      if (closing || guard.pending) return;
      closing = true;
      if (hasOverlay() && history.state?.participantAddOverlay) history.back();
      else {
        const url = overlayUrl();
        url.searchParams.delete("addParticipant");
        const state = { ...(history.state || {}) };
        delete state.participantAddBase;
        delete state.participantAddOverlay;
        history.replaceState(state, "", url);
        hide();
        closing = false;
      }
    }

    const sync = () => {
      if (restoringHistory && hasOverlay()) {
        restoringHistory = false;
        if (!guard.pending && !completedWithoutEditor && guard.dirtyForms().length) guard.confirmDiscard(closeNow);
        return;
      }
      if (!hasOverlay() && dialog?.open && !closing && (guard.pending || (!completedWithoutEditor && guard.dirtyForms().length))) {
        restoringHistory = true;
        history.forward();
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
      event.preventDefault();
      opener = trigger;
      parentRefreshFailed = false;
      completedWithoutEditor = false;
      show(true);
    });
    window.addEventListener("popstate", sync);
    participantAddDialogCleanup = cleanupBeforeUpdate;

    const replaceParentAfterSuccess = (parsed, destination) => {
      const nextPage = parsed.querySelector(".event-participants-page");
      const nextNotice = parsed.querySelector("#app-notice-region");
      const currentPage = page instanceof HTMLElement ? page : document.querySelector(".event-participants-page");
      if (!(nextPage instanceof HTMLElement) || !(nextNotice instanceof HTMLElement) || !(currentPage instanceof HTMLElement)) {
        parentRefreshFailed = true;
        completedWithoutEditor = true;
        guard.showFailure(adminText("adminParentRefreshError"));
        return false;
      }

      const scroll = { left: window.scrollX, top: window.scrollY };
      const currentNotice = document.querySelector("#app-notice-region");
      const main = document.querySelector("main#main-content");
      if (currentNotice && dialog?.contains(currentNotice) && main) {
        if (typeof main.prepend === "function") main.prepend(currentNotice);
        else main.append(currentNotice);
      }
      const next = document.importNode(nextPage, true);
      const nextTrigger = next.querySelector("[data-participant-add-trigger]");
      dispose();
      currentPage.replaceWith(next);
      const noticeRegion = document.querySelector("#app-notice-region");
      if (noticeRegion) noticeRegion.replaceChildren(...Array.from(nextNotice.childNodes, node => document.importNode(node, true)));
      const finalUrl = new URL(destination.href);
      finalUrl.searchParams.delete("addParticipant");
      const state = { ...(history.state || {}) };
      delete state.participantAddBase;
      delete state.participantAddOverlay;
      history.replaceState(state, "", finalUrl);
      initializeParticipantWorkspace(next);
      document.dispatchEvent(new CustomEvent("bingo:content-updated", { detail: { selectors: ["#app-notice-region", ".event-participants-page"] } }));
      window.initializeTransientToastLayer?.();
      window.scrollTo?.(scroll);
      window.setTimeout(() => nextTrigger?.focus({ preventScroll: true }), 0);
      return true;
    };

    const submitParticipantAddForm = async (form, submitter) => {
      if (guard.pending || completedWithoutEditor) return;
      const body = new FormData(form);
      if (submitter?.name) body.append(submitter.name, submitter.value);
      const action = submitter?.getAttribute("formaction") || form.action || currentUrl().href;
      const finish = guard.begin(form);
      try {
        const response = await window.fetch(action, {
          method: "POST",
          body,
          credentials: "same-origin",
          headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        if (!response.ok) throw new Error("Participant creation request failed.");
        const destination = new URL(response.url || action, window.location.href);
        if (destination.origin !== window.location.origin || destination.pathname !== currentUrl().pathname) {
          dispose();
          window.location.assign(destination.href);
          return;
        }
        const html = await response.text();
        const parsed = new DOMParser().parseFromString(html, "text/html");
        if (parsed.querySelector("[data-wom-validation-confirmation]")) {
          replaceEditor(html);
          content.querySelector("[data-wom-validation-cancel]")?.focus({ preventScroll: true });
          return;
        }
        if (parsed.querySelector("[data-wom-validation-cancelled]")) {
          replaceEditor(html);
          content.querySelector("[data-wom-validation-normal-submit]")?.focus({ preventScroll: true });
          return;
        }
        const notice = parsed.querySelector("#app-notice-region");
        const success = notice?.querySelector(".app-toast-success");
        if (!(success instanceof HTMLElement)) {
          const failure = notice?.querySelector(".app-toast-error, .app-toast-warning");
          const message = failure?.querySelector(".app-toast-copy span")?.textContent.trim() || parsed.querySelector("[data-toast-host] [data-toast].is-error [data-component-text]")?.textContent.trim();
          guard.showFailure(message || adminText("adminPostError"));
          return;
        }
        const refreshed = replaceParentAfterSuccess(parsed, destination);
        if (!refreshed) {
          finish();
          guard.initialize();
        }
      } catch {
        guard.showFailure(adminText("adminPostError"));
      } finally {
        finish();
      }
    };

    if (hasOverlay()) {
      setRouteVisibility(true);
      const base = overlayUrl();
      base.searchParams.delete("addParticipant");
      history.replaceState({ ...(history.state || {}), participantAddBase: true }, "", base);
      const reopened = new URL(window.location.href);
      reopened.searchParams.set("addParticipant", "1");
      history.pushState({ ...(history.state || {}), participantAddOverlay: true }, "", reopened);
      show(false);
    }
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

  function initializeDatePickers(root) {
    if (typeof window.flatpickr !== "function") return;
    root.querySelectorAll("[data-event-manage-datetime-picker]").forEach((input) => {
      if (input._flatpickr) return;
      window.initializeBingoDateTimePicker(input, {
        dateFormat: "Y-m-d\\TH:i",
        altInput: true,
        altFormat: "d/m/Y H:i",
        defaultDate: input.value,
        timeLabel: input.dataset.timeLabel || adminText("adminTime")
      });
    });
  }

})();

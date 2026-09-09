(() => {
  "use strict";

  let dialog = null;
  let content = null;
  let opener = null;
  let directRoute = false;
  let closing = false;
  let loadId = 0;

  let page = document.querySelector("[data-catalogue-page]");
  if (!(page instanceof HTMLElement)) return;
  const adminText = key => document.body?.dataset[key] || "";

  const main = document.querySelector("main#main-content");
  let parentUrl = null;
  let parentScroll = 0;
  let parentRefreshFailed = false;
  let staleEditor = false;
  let completedWithoutEditor = false;
  let restoringHistory = false;
  let suspendedConfirmation = null;
  const currentEditor = () => content?.querySelector("[data-catalogue-editor]") || page.querySelector("[data-catalogue-editor]");
  const guard = window.createAdminEditorGuard({
    editor: currentEditor, prefix: "catalogue-editor", saveError: () => adminText("signupQuestionsSaveError"),
    closeConfirmation: () => {
      suspendedConfirmation = getOpenConfirmation();
      if (suspendedConfirmation) closeConfirmation(suspendedConfirmation, false);
    },
    restoreConfirmation: () => {
      if (suspendedConfirmation) {
        if (suspendedConfirmation.matches("details")) suspendedConfirmation.open = true;
        else {
          suspendedConfirmation.hidden = false;
          if (suspendedConfirmation.catalogueOpener?.matches("button")) suspendedConfirmation.catalogueOpener.hidden = true;
        }
        suspendedConfirmation.scrollIntoView({ block: "nearest", behavior: "instant" });
      }
      suspendedConfirmation = null;
    }
  });
  const showFailure = (message = adminText("signupQuestionsSaveError"), type = "error") => {
    guard.showFailure(message);
    window.showBingoToast?.(message, type);
  };
  const guarded = (action, form) => {
    if (guard.pending) return;
    if (guard.dirtyForms(form).length) guard.confirmDiscard(action);
    else action();
  };
  const currentUrl = () => new URL(window.location.href);
  const hasOverlay = () => currentUrl().searchParams.get("overlay") === "1";
  const editorUrl = (source = window.location.href) => {
    const url = new URL(source, window.location.href);
    url.searchParams.set("overlay", "1");
    return url.href;
  };
  const baseUrl = (source = window.location.href) => {
    const url = new URL(source, window.location.href);
    url.searchParams.delete("bossId");
    url.searchParams.delete("addBoss");
    url.searchParams.delete("dropId");
    url.searchParams.delete("overlay");
    return url;
  };

  function build() {
    if (dialog && document.body.contains(dialog)) return;
    dialog = document.createElement("dialog");
    dialog.id = "catalogue-editor-dialog";
    dialog.className = "admin-route-dialog catalogue-route-dialog";
    content = document.createElement("div");
    content.className = "admin-route-dialog-content";
    dialog.append(content);
    (main || document.body).append(dialog);
    dialog.addEventListener("cancel", event => {
      if (guard.pending || guard.cancelDiscard()) { event.preventDefault(); return; }
      const confirmation = getOpenConfirmation();
      if (confirmation) { event.preventDefault(); closeConfirmation(confirmation); return; }
      event.preventDefault();
      closeWithHistory();
    });
    dialog.addEventListener("keydown", event => {
      if (event.key !== "Escape") return;
      if (guard.pending || guard.cancelDiscard()) { event.preventDefault(); return; }
      const confirmation = getOpenConfirmation();
      if (confirmation) {
        event.preventDefault();
        event.stopPropagation();
        closeConfirmation(confirmation);
        return;
      }
      event.preventDefault();
      closeWithHistory();
    });
    dialog.addEventListener("click", event => {
      if (event.target !== dialog) return;
      if (guard.pending || guard.cancelDiscard()) { event.preventDefault(); return; }
      const confirmation = getOpenConfirmation();
      if (confirmation) { closeConfirmation(confirmation); return; }
      closeWithHistory();
    });
  }

  function getOpenConfirmation() {
    const editor = currentEditor();
    return editor?.querySelector("details.catalogue-confirmation-box[open]") || [...(editor?.querySelectorAll("[data-catalogue-duplicate-confirmation]") || [])].find(item => !item.hidden);
  }

  function replaceEditor(html) {
    const parsed = new DOMParser().parseFromString(html, "text/html");
    const cataloguePage = parsed.querySelector("[data-catalogue-page]");
    const editor = cataloguePage?.querySelector("[data-catalogue-editor]");
    if (!(cataloguePage instanceof HTMLElement) || !(editor instanceof HTMLElement)) throw new Error("Catalogue editor was not returned.");
    content.replaceChildren(document.importNode(cataloguePage, true));
    bindEditor();
  }

  function bindEditor() {
    const editor = currentEditor();
    if (!(editor instanceof HTMLElement)) return;
    dialog?.setAttribute("aria-labelledby", "catalogue-editor-title");
    dialog?.setAttribute("aria-describedby", "catalogue-editor-description");
    const close = editor.querySelector("[data-catalogue-close]");
    if (close instanceof HTMLElement && close.dataset.catalogueCloseReady !== "true") {
      close.dataset.catalogueCloseReady = "true";
      close.addEventListener("click", closeWithHistory);
    }
    editor.querySelectorAll("details.catalogue-confirmation-box").forEach(confirmation => {
      if (confirmation.dataset.confirmationReady === "true") return;
      confirmation.dataset.confirmationReady = "true";
      confirmation.querySelector("summary")?.addEventListener("click", event => {
        event.preventDefault();
        if (guard.pending) return;
        if (confirmation.open) { closeConfirmation(confirmation); return; }
        guarded(() => {
          const previous = getOpenConfirmation();
          if (previous) closeConfirmation(previous, false);
          confirmation.open = true;
          confirmation.scrollIntoView({ block: "nearest", behavior: "instant" });
          confirmation.querySelector("[data-catalogue-confirmation-cancel]")?.focus({ preventScroll: true });
        }, confirmation.querySelector("form"));
      });
      confirmation.querySelector("[data-catalogue-confirmation-cancel]")?.addEventListener("click", event => {
        event.preventDefault();
        closeConfirmation(confirmation);
      });
      confirmation.addEventListener("keydown", event => {
        if (event.key !== "Escape" || !confirmation.open) return;
        event.preventDefault();
        event.stopPropagation();
        closeConfirmation(confirmation);
      });
      confirmation.addEventListener("click", event => {
        if (event.target === confirmation && confirmation.open) closeConfirmation(confirmation);
      });
    });
    const reload = editor.querySelector("[data-catalogue-reload]");
    if (reload && reload.dataset.reloadReady !== "true") {
      reload.dataset.reloadReady = "true";
      reload.addEventListener("click", () => guarded(() => { guard.initialize(); window.location.reload(); }));
    }
    bindEditorInputs(editor);
    editor.querySelectorAll("form").forEach(form => {
      if (form.dataset.catalogueSubmitReady === "true") return;
      form.dataset.catalogueSubmitReady = "true";
      form.addEventListener("submit", event => {
        event.stopPropagation();
        if (event.defaultPrevented) return;
        event.preventDefault();
        guarded(() => submitEditor(form), form);
      });
    });
    guard.initialize();
  }

  function closeConfirmation(confirmation, restoreFocus = true) {
    if (guard.pending) return;
    if (!confirmation.matches("details")) {
      confirmation.hidden = true;
      if (confirmation.catalogueOpener?.matches("button")) confirmation.catalogueOpener.hidden = false;
      if (restoreFocus) confirmation.catalogueOpener?.focus({ preventScroll: true });
      return;
    }
    confirmation.removeAttribute("open");
    confirmation.open = false;
    if (restoreFocus) confirmation.querySelector("summary")?.focus({ preventScroll: true });
  }

  function bindDropEditors(editor) {
    const rows = [...editor.querySelectorAll("[data-catalogue-drop-row]")];
    const setExpanded = (row, expanded) => {
      row.dataset.catalogueDropExpanded = expanded ? "true" : "false";
      const panel = row.querySelector("[data-catalogue-drop-editor]");
      const toggle = row.querySelector("[data-catalogue-drop-toggle]");
      if (panel instanceof HTMLElement) panel.hidden = !expanded;
      if (toggle instanceof HTMLElement) toggle.setAttribute("aria-expanded", expanded ? "true" : "false");
    };
    rows.forEach(row => {
      const toggle = row.querySelector("[data-catalogue-drop-toggle]");
      if (!(toggle instanceof HTMLElement)) return;
      if (toggle.dataset.dropToggleReady === "true") return;
      setExpanded(row, row.dataset.catalogueDropInitialExpanded === "true");
      toggle.dataset.dropToggleReady = "true";
      toggle.addEventListener("click", () => {
        const expanded = row.dataset.catalogueDropExpanded === "true";
        if (guard.pending) return;
        const collapse = () => {
          rows.forEach(other => {
            if (!guard.dirtyForms().some(form => other.contains(form))) setExpanded(other, false);
          });
          setExpanded(row, !expanded);
        };
        // Keep other dirty drops visible. Collapsing this dirty drop needs consent.
        if (expanded && guard.dirtyForms().some(form => row.contains(form))) guard.confirmDiscard(() => { row.querySelectorAll("form").forEach(form => form.reset()); collapse(); });
        else collapse();
      });
    });
  }

  function bindRateLabel(root) {
    const category = root.querySelector("[data-catalogue-rate-category]");
    const label = root.querySelector("[data-catalogue-rate-label]");
    if (!(category instanceof HTMLSelectElement) || !(label instanceof HTMLElement)) return;
    const update = () => { label.textContent = category.value === "Minigame" ? adminText("adminCatalogueRunsPerHour") : adminText("adminCatalogueKillsPerHour"); };
    if (category.dataset.rateLabelReady !== "true") {
      category.dataset.rateLabelReady = "true";
      category.addEventListener("change", update);
    }
    update();
  }

  function bindEditorInputs(editor) {
    bindDropEditors(editor);
    bindRateLabel(editor);
    editor.querySelectorAll(".catalogue-display-rate-input").forEach(input => {
      if (input.dataset.rateReady === "true") return;
      input.dataset.rateReady = "true";
      input.addEventListener("input", () => {
        const parsed = parseDisplayedRate(input.value);
        if (!parsed) return;
        const form = input.form;
        const probability = form?.querySelector(".catalogue-probability-input");
        const rolls = form?.querySelector("[name='rollsPerCompletion'], [name='BossDrop.RollsPerCompletion']");
        if (probability) probability.value = String(parsed.probability);
        if (rolls) rolls.value = String(parsed.rolls);
      });
    });
    editor.querySelectorAll("input[data-original-item-name]").forEach(input => {
      if (input.dataset.nameReady === "true") return;
      input.dataset.nameReady = "true";
      const form = input.form;
      const duplicateConfirmation = form?.querySelector("[data-catalogue-duplicate-confirmation]");
      if (duplicateConfirmation instanceof HTMLElement && duplicateConfirmation.dataset.confirmationReady !== "true") {
        duplicateConfirmation.dataset.confirmationReady = "true";
        duplicateConfirmation.addEventListener("keydown", event => {
          if (event.key !== "Escape" || duplicateConfirmation.hidden) return;
          event.preventDefault();
          event.stopPropagation();
          closeConfirmation(duplicateConfirmation);
        });
        duplicateConfirmation.querySelector("[data-catalogue-duplicate-cancel]")?.addEventListener("click", () => {
          input.value = input.dataset.originalItemName;
          closeConfirmation(duplicateConfirmation, false);
          form.requestSubmit();
        });
        duplicateConfirmation.querySelector("[data-catalogue-duplicate-confirm]")?.addEventListener("click", () => {
          form.dataset.duplicateConfirmed = "true";
          closeConfirmation(duplicateConfirmation, false);
          form.requestSubmit();
        });
      }
      form?.addEventListener("submit", event => {
        if (guard.pending) { event.preventDefault(); return; }
        const proposed = input.value.trim();
        const duplicate = [...editor.querySelectorAll("input[data-original-item-name]")]
          .some(other => other !== input && normalizeItemName(other.dataset.originalItemName) === normalizeItemName(proposed));
        const useExisting = form.querySelector("[data-use-existing-item]");
        if (!useExisting) return;
        if (form.dataset.duplicateConfirmed === "true") {
          delete form.dataset.duplicateConfirmed;
          useExisting.value = "true";
          return;
        }
        useExisting.value = "false";
        if (proposed !== input.dataset.originalItemName && duplicate && duplicateConfirmation instanceof HTMLElement) {
          event.preventDefault();
          const previous = getOpenConfirmation();
          if (previous) closeConfirmation(previous, false);
          duplicateConfirmation.catalogueOpener = event.submitter || input;
          duplicateConfirmation.hidden = false;
          if (duplicateConfirmation.catalogueOpener.matches("button")) duplicateConfirmation.catalogueOpener.hidden = true;
          duplicateConfirmation.scrollIntoView({ block: "nearest", behavior: "instant" });
          duplicateConfirmation.querySelector("[data-catalogue-duplicate-cancel]")?.focus({ preventScroll: true });
        }
      });
    });
  }

  async function refreshWorkspace() {
    try {
      const response = await window.fetch(parentUrl || baseUrl().href, { credentials: "same-origin" });
      const parsed = new DOMParser().parseFromString(await response.text(), "text/html");
      const next = parsed.querySelector("[data-catalogue-page][data-catalogue-editor-route='false']");
      const previous = main.querySelector("[data-catalogue-page][data-catalogue-editor-route='false']");
      if (!response.ok || !next || !previous) throw new Error("Catalogue workspace refresh failed.");
      const href = opener?.getAttribute("href");
      const replacement = document.importNode(next, true);
      previous.replaceWith(replacement);
      initializeWorkspace(replacement);
      opener = [...replacement.querySelectorAll("[data-catalogue-record], [data-catalogue-add]")].find(item => item.getAttribute("href") === href) || replacement.querySelector("[data-catalogue-add]");
      parentRefreshFailed = false;
      return true;
    } catch { parentRefreshFailed = true; return false; }
  }

  async function submitEditor(form) {
    if (guard.pending || staleEditor || completedWithoutEditor) return;
    const data = new FormData(form);
    const finish = guard.begin(form);
    try {
      const response = await window.fetch(form.action || currentUrl().href, { method: "POST", body: data, credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } });
      if (!response.ok) throw new Error("Catalogue save failed.");
      const html = await response.text();
      const parsed = new DOMParser().parseFromString(html, "text/html");
      const result = parsed.querySelector("[data-catalogue-page]");
      if (!result) throw new Error("Catalogue response missing.");
      const message = result.dataset.catalogueStatusMessage || "";
      const type = (result.dataset.catalogueStatusType || "").toLowerCase();
      if (type === "error" || type === "warning") {
        const recordId = data.get("recordId");
        const submittedVersion = data.get("expectedVersion");
        const currentForm = [...result.querySelectorAll("form")].find(candidate => candidate.querySelector("input[name='recordId']")?.value === recordId);
        staleEditor = !!(submittedVersion && (!currentForm || currentForm.querySelector("input[name='expectedVersion']")?.value !== submittedVersion));
        showFailure(staleEditor ? adminText("adminCatalogueStaleError") : message, type);
        if (staleEditor) currentEditor().querySelector("[data-catalogue-reload]").hidden = false;
        return;
      }
      const editorShell = result.querySelector("[data-catalogue-editor]");
      const nextEditor = editorShell?.querySelector("form") ? editorShell : null;
      if (!dialog?.open) {
        if (!nextEditor) {
          finish();
          guard.initialize();
          if (message) sessionStorage.setItem("bingo:pending-toast", JSON.stringify({ message, type: "success" }));
          window.location.replace(baseUrl().href);
          return;
        }
        const replacement = document.importNode(result, true);
        page.replaceWith(replacement);
        page = replacement;
        history.replaceState(history.state, "", response.url || currentUrl().href);
        if (page.querySelector("[data-catalogue-editor]")) bindEditor();
        else initializeWorkspace(page);
        if (message) window.showBingoToast?.(message, "success");
        return;
      }
      if (nextEditor) { replaceEditor(html); history.replaceState(history.state, "", response.url || currentUrl().href); }
      const refreshed = await refreshWorkspace();
      finish();
      if (!refreshed) {
        if (!nextEditor) { completedWithoutEditor = true; guard.initialize(); }
        showFailure(adminText("adminCatalogueParentRefreshError"), "warning");
        return;
      }
      if (!nextEditor) { history.replaceState({}, "", parentUrl || baseUrl().href); hide(); }
      if (message) window.showBingoToast?.(message, "success");
    } catch { showFailure(); }
    finally { finish(); }
  }

  function parseDisplayedRate(value) {
    const match = value.trim().match(/^(?:(\d+)\s*[x×*]\s*)?(\d+(?:[.,]\d+)?)\s*\/\s*([\d., ]+?)(?:\s*[x×*]\s*(\d+)\s*(?:rolls?)?)?$/i);
    if (!match || (match[1] && match[4])) return null;
    const number = text => {
      const cleaned = text.replaceAll(" ", "");
      const normalized = /^\d{1,3}(?:[,.]\d{3})+$/.test(cleaned) ? cleaned.replaceAll(",", "").replaceAll(".", "") : cleaned.replace(",", ".");
      return Number(normalized);
    };
    const numerator = number(match[2]);
    const denominator = number(match[3]);
    const rolls = Number(match[1] || match[4] || 1);
    if (!(numerator > 0) || !(denominator > 0) || numerator / denominator > 1 || !(rolls >= 1)) return null;
    return { probability: numerator / denominator, rolls };
  }

  const normalizeItemName = value => value.trim().toLocaleUpperCase();

  async function loadDirectWorkspace() {
    if (!directRoute || !(main instanceof HTMLElement)) return;
    main.hidden = true;
    parentUrl = parentUrl || baseUrl().href;
    const requestedEditor = editorUrl();
    const [workspaceResult, editorResult] = await Promise.allSettled([
      window.fetch(baseUrl().href, { credentials: "same-origin" }),
      window.fetch(requestedEditor, { credentials: "same-origin" })
    ]);
    if (workspaceResult.status !== "fulfilled" || !workspaceResult.value.ok) throw new Error("Catalogue workspace request failed.");
    const workspaceHtml = await workspaceResult.value.text();
    const workspaceDocument = new DOMParser().parseFromString(workspaceHtml, "text/html");
    const workspace = workspaceDocument.querySelector("[data-catalogue-page][data-catalogue-editor-route='false']");
    if (!(workspace instanceof HTMLElement)) throw new Error("Catalogue workspace was not returned.");
    main.replaceChildren(document.importNode(workspace, true));
    directRoute = false;
    initializeWorkspace(main);
    main.hidden = false;
    if (editorResult.status !== "fulfilled" || !editorResult.value.ok) throw new Error("Catalogue editor request failed.");
    build();
    replaceEditor(await editorResult.value.text());
  }

  async function prepareAndShow() {
    const requestId = ++loadId;
    try {
      if (directRoute) await loadDirectWorkspace();
      else {
        build();
        const response = await window.fetch(editorUrl(opener?.getAttribute("href") || window.location.href), { credentials: "same-origin" });
        if (!response.ok) throw new Error("Catalogue editor request failed.");
        replaceEditor(await response.text());
      }
      if (requestId !== loadId || !hasOverlay()) return;
      if (!dialog || !document.body.contains(dialog)) throw new Error("Catalogue dialog is not connected.");
      if (!dialog.open) dialog.showModal();
      document.body.classList.add("admin-route-dialog-open");
      window.setTimeout(() => content.querySelector("[data-catalogue-close], input, select, textarea")?.focus({ preventScroll: true }), 0);
    } catch {
      if (requestId !== loadId || !hasOverlay()) return;
      if (directRoute && main instanceof HTMLElement) main.hidden = false;
      if (!dialog || !document.body.contains(dialog)) build();
      content.replaceChildren(Object.assign(document.createElement("p"), { textContent: adminText("adminCatalogueLoadError"), role: "alert" }));
      const close = document.createElement("button");
      close.type = "button";
      close.className = "admin-button-secondary";
      close.textContent = adminText("adminCancel");
      close.addEventListener("click", closeWithHistory);
      content.append(close);
      if (!dialog.open) dialog.showModal();
      document.body.classList.add("admin-route-dialog-open");
    }
  }

  document.addEventListener("bingo:content-updated", event => {
    if (!dialog || !document.body.contains(dialog)) return;
    const selectors = event.detail?.selectors ?? [];
    if (selectors.includes(".catalogue-editor-component")) bindEditor();
  });

  function hide(restoreFocus = true) {
    loadId++;
    if (dialog?.open) dialog.close();
    content?.replaceChildren();
    dialog?.remove();
    dialog = null;
    content = null;
    document.body.classList.remove("admin-route-dialog-open");
    const focusTarget = opener;
    if (restoreFocus) window.setTimeout(() => focusTarget instanceof HTMLElement && document.body.contains(focusTarget) && focusTarget.focus({ preventScroll: true }), 0);
    opener = null;
    window.scrollTo?.(0, parentScroll);
    if (parentRefreshFailed) {
      const url = parentUrl || baseUrl().href;
      if (url === currentUrl().href) window.location.reload();
      else window.location.replace(url);
    }
  }

  function closeWithHistory(event) {
    event?.preventDefault();
    if (guard.pending || guard.cancelDiscard()) return;
    guarded(closeNow);
  }

  function closeNow() {
    if (closing || guard.pending) return;
    closing = true;
    if (hasOverlay() && history.state?.catalogueOverlay) history.back();
    else { history.replaceState({}, "", baseUrl()); hide(); closing = false; }
  }

  function sync() {
    if (restoringHistory && hasOverlay()) { restoringHistory = false; return; }
    if (!hasOverlay() && dialog?.open && !closing && (guard.pending || guard.dirtyForms().length)) {
      restoringHistory = true;
      history.forward();
      if (!guard.pending) guard.confirmDiscard(closeNow);
      return;
    }
    if (hasOverlay()) { if (!dialog?.open) prepareAndShow(); }
    else if (dialog) hide(true);
    closing = false;
  }

  function initializeWorkspace(root) {
    const search = root.querySelector("#boss-search");
    const clear = root.querySelector("[data-admin-search-clear]");
    const records = [...root.querySelectorAll("[data-catalogue-record]")];
    const filter = root.querySelector("[data-catalogue-category-filter]");
    const form = root.querySelector("form[data-catalogue-directory-search]");
    const empty = root.querySelector("#no-boss-results");
    if (!(search instanceof HTMLInputElement) || !(filter instanceof HTMLSelectElement) || !empty) return;
    let activeFilter = "all";
    const storageKey = `catalogue:${location.pathname}`;
    try {
      const saved = JSON.parse(sessionStorage.getItem(storageKey) || "{}");
      search.value = saved.search || "";
      activeFilter = saved.filter || "all";
    } catch { }
    if (![...filter.options].some(option => option.value === activeFilter)) activeFilter = "all";
    filter.value = activeFilter;
    const applyFilters = () => {
      const term = search.value.trim().toLowerCase();
      let visible = 0;
      records.forEach(record => {
        const matches = (!term || record.dataset.search.includes(term)) && (activeFilter === "all" || (activeFilter === "inactive" && record.dataset.active === "false") || record.dataset.category === activeFilter);
        record.hidden = !matches;
        if (matches) visible++;
      });
      filter.value = activeFilter;
      empty.hidden = visible !== 0;
      if (clear instanceof HTMLElement) clear.hidden = search.value.trim().length === 0;
      sessionStorage.setItem(storageKey, JSON.stringify({ search: search.value, filter: activeFilter }));
    };
    search.addEventListener("input", applyFilters);
    filter.addEventListener("change", () => { activeFilter = filter.value; applyFilters(); });
    form?.addEventListener("submit", event => { event.preventDefault(); applyFilters(); });
    clear?.addEventListener("click", () => { search.value = ""; applyFilters(); search.focus(); });
    records.forEach(record => record.addEventListener("click", event => {
      event.preventDefault();
      parentUrl = currentUrl().href; parentScroll = window.scrollY; parentRefreshFailed = false; staleEditor = false; completedWithoutEditor = false;
      opener = record;
      history.pushState({ ...(history.state || {}), catalogueOverlay: true }, "", editorUrl(record.dataset.editorUrl));
      prepareAndShow();
    }));
    const add = root.querySelector("[data-catalogue-add]");
    add?.addEventListener("click", event => {
      event.preventDefault();
      parentUrl = currentUrl().href; parentScroll = window.scrollY; parentRefreshFailed = false; staleEditor = false; completedWithoutEditor = false;
      opener = add;
      history.pushState({ ...(history.state || {}), catalogueOverlay: true }, "", editorUrl(add.getAttribute("href")));
      prepareAndShow();
    });
    applyFilters();
  }

  const editor = page.querySelector("[data-catalogue-editor]");
  bindRateLabel(page);
  if (editor instanceof HTMLElement) bindDropEditors(editor);
  const recordsPage = page.getAttribute("data-catalogue-editor-route") !== "true";
  directRoute = editor instanceof HTMLElement && !recordsPage;
  if (recordsPage) initializeWorkspace(document);
  window.addEventListener("popstate", sync);
  window.addEventListener("beforeunload", event => { if (guard.pending || guard.dirtyForms().length) { event.preventDefault(); event.returnValue = ""; } });

  if (directRoute && hasOverlay()) {
    const sourceUrl = currentUrl();
    const base = baseUrl(sourceUrl.href);
    history.replaceState({}, "", base.href);
    const source = page.getAttribute("data-catalogue-url") || base.href;
    const route = new URL(source, base.href);
    const originalParams = sourceUrl.searchParams;
    originalParams.forEach((value, key) => { if (["bossId", "addBoss", "dropId"].includes(key)) route.searchParams.set(key, value); });
    route.searchParams.set("overlay", "1");
    history.pushState({ catalogueOverlay: true }, "", route.href);
    prepareAndShow();
  } else if (hasOverlay()) sync();
  else if (editor instanceof HTMLElement) { directRoute = false; bindEditor(); }
})();

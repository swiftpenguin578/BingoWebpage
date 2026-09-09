(() => {
  "use strict";

  const main = document.querySelector("main#main-content");
  if (!(main instanceof HTMLElement)) return;
  const adminText = key => document.body?.dataset[key] || "";
  let directory = document.querySelector(".admin-accounts-page");
  let dialog = null;
  let content = null;
  let confirmationDialog = null;
  let confirmationForm = null;
  let confirmationOpener = null;
  let opener = null;
  let closing = false;
  let loadId = 0;
  let directBootstrap = false;

  const triggerSelector = "[data-account-manage-trigger='true'], [data-account-create-trigger='true']";
  let parentRefreshFailed = false;
  let parentUrl = null;
  let parentScroll = 0;
  let restoringHistory = false;
  let suspendedConfirmation = null;
  let suspendedOpener = null;
  const guard = window.createAdminEditorGuard({
    editor: () => content, prefix: "account-editor", saveError: () => adminText("adminAccountActionError"),
    closeConfirmation: () => {
      suspendedOpener = confirmationOpener;
      suspendedConfirmation = hasConfirmation() ? confirmationDialog : content?.querySelector("details.admin-account-action-confirmation[open]");
      closeConfirmation(false);
    },
    restoreConfirmation: () => {
      if (suspendedConfirmation && suspendedConfirmation === confirmationDialog) confirmationDialog.hidden = false;
      else if (suspendedConfirmation) suspendedConfirmation.open = true;
      confirmationOpener = suspendedOpener;
      if (confirmationOpener && hasConfirmation()) confirmationOpener.hidden = true;
      suspendedConfirmation = null;
    }
  });
  const guarded = (action, form) => {
    if (guard.pending || (parentRefreshFailed && content?.querySelector("[data-account-create-page]"))) return;
    if (guard.dirtyForms(form).length) guard.confirmDiscard(action);
    else action();
  };
  const currentUrl = () => new URL(window.location.href);
  const hasOverlay = () => currentUrl().searchParams.get("overlay") === "1";
  const overlayUrl = (source) => {
    const url = new URL(source, window.location.href);
    url.searchParams.set("overlay", "1");
    return url.href;
  };
  const hasConfirmation = () => confirmationDialog instanceof HTMLElement && !confirmationDialog.hidden;

  const build = () => {
    if (!(dialog instanceof HTMLDialogElement) && hasOverlay()) {
      dialog = document.createElement("dialog");
      dialog.className = "admin-route-dialog account-manage-route-dialog";
      content = document.createElement("div");
      content.className = "admin-route-dialog-content";
      dialog.append(content);
      (main || document.body).append(dialog);
      dialog.addEventListener("cancel", (event) => {
        if (guard.pending || guard.cancelDiscard()) { event.preventDefault(); return; }
        if (hasConfirmation()) {
          event.preventDefault();
          closeConfirmation();
          return;
        }
        event.preventDefault();
        closeWithHistory();
      });
      dialog.addEventListener("keydown", (event) => {
        if (event.key !== "Escape") return;
        if (guard.pending || guard.cancelDiscard()) { event.preventDefault(); return; }
        event.preventDefault();
        if (guard.pending || guard.cancelDiscard()) return;
        if (hasConfirmation()) closeConfirmation();
        else closeWithHistory();
      });
      dialog.addEventListener("click", (event) => {
        if (event.target !== dialog || guard.pending || guard.cancelDiscard()) return;
        if (hasConfirmation()) closeConfirmation();
        else closeWithHistory();
      });
    }

    if (confirmationDialog instanceof HTMLElement) return;
    confirmationDialog = document.createElement("div");
    confirmationDialog.hidden = true;
    confirmationDialog.setAttribute("role", "group");
    confirmationDialog.id = "admin-account-confirmation-dialog";
    confirmationDialog.className = "event-confirmation-box admin-account-inline-confirmation";
    confirmationDialog.setAttribute("aria-labelledby", "admin-account-confirmation-title");
    confirmationDialog.setAttribute("aria-describedby", "admin-account-confirmation-support");

    const surface = document.createElement("div");
    surface.className = "admin-account-confirmation-surface";
    const heading = document.createElement("div");
    heading.className = "admin-account-confirmation-heading";
    const title = document.createElement("h2");
    title.id = "admin-account-confirmation-title";
    const close = document.createElement("button");
    close.type = "button";
    close.className = "admin-route-dialog-close";
    close.setAttribute("aria-label", adminText("adminCloseConfirmation"));
    close.dataset.accountConfirmationClose = "true";
    close.innerHTML = "<svg aria-hidden=\"true\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-linecap=\"round\" stroke-linejoin=\"round\" stroke-width=\"2\"><path d=\"m6 6 12 12\" /><path d=\"m18 6-12 12\" /></svg>";
    heading.append(title, close);

    const support = document.createElement("p");
    support.id = "admin-account-confirmation-support";
    const form = document.createElement("form");
    form.method = "post";
    const actions = document.createElement("div");
    actions.className = "admin-account-confirmation-actions";
    const cancel = document.createElement("button");
    cancel.type = "button";
    cancel.className = "admin-button-secondary";
    cancel.textContent = adminText("adminCancel");
    cancel.dataset.accountConfirmationCancel = "true";
    const confirm = document.createElement("button");
    confirm.type = "submit";
    confirm.dataset.accountConfirmationSubmit = "true";
    actions.append(cancel, confirm);
    form.append(actions);
    surface.append(heading, support, form);
    confirmationDialog.append(surface);


    close.addEventListener("click", (event) => { event.preventDefault(); closeConfirmation(); });
    cancel.addEventListener("click", (event) => { event.preventDefault(); closeConfirmation(); });
    confirmationDialog.addEventListener("cancel", (event) => { event.preventDefault(); closeConfirmation(); });
    confirmationDialog.addEventListener("keydown", (event) => {
      if (event.key !== "Escape") return;
      event.preventDefault();
      event.stopPropagation();
      closeConfirmation();
    });
    confirmationDialog.addEventListener("click", (event) => {
      if (event.target === confirmationDialog) closeConfirmation();
    });
    form.addEventListener("submit", submitConfirmation);
    confirmationForm = form;
  };

  const closeDetailsConfirmation = (confirmation) => {
    confirmation.removeAttribute("open");
    confirmation.open = false;
    confirmation.querySelector("summary")?.focus({ preventScroll: true });
  };

  function closeConfirmation(restoreFocus = true) {
    const trigger = confirmationOpener;
    if (guard.pending) return;
    if (confirmationDialog) confirmationDialog.hidden = true;
    if (trigger) trigger.hidden = false;
    content?.querySelectorAll("details.admin-account-action-confirmation").forEach(item => { item.open = false; });
    confirmationForm?.removeAttribute("aria-busy");
    confirmationOpener = null;
    if (restoreFocus && trigger instanceof HTMLElement && document.body.contains(trigger)) trigger.focus({ preventScroll: true });
  }

  const bindConfirmation = (confirmation) => {
    if (!(confirmation instanceof HTMLDetailsElement) || confirmation.dataset.accountConfirmationBound === "true") return;
    confirmation.dataset.accountConfirmationBound = "true";
    confirmation.addEventListener("toggle", () => {
      if (!confirmation.open) return;
      if (guard.pending) { confirmation.open = false; return; }
      if (hasConfirmation() || guard.dirtyForms(confirmation.querySelector("form")).length) {
        confirmation.open = false;
        guarded(() => { closeConfirmation(false); confirmation.open = true; guard.initialize(); }, confirmation.querySelector("form"));
        return;
      }
      content.querySelectorAll("details.admin-account-action-confirmation").forEach(item => { if (item !== confirmation) item.open = false; });
      window.setTimeout(() => confirmation.querySelector("[data-account-confirmation-cancel]")?.focus({ preventScroll: true }), 0);
    });
    confirmation.addEventListener("keydown", (event) => {
      if (event.key !== "Escape" || !confirmation.open) return;
      if (guard.pending) { event.preventDefault(); event.stopPropagation(); return; }
      event.preventDefault();
      event.stopPropagation();
      closeDetailsConfirmation(confirmation);
    });
    confirmation.addEventListener("click", (event) => {
      if (event.target === confirmation && confirmation.open) closeDetailsConfirmation(confirmation);
    });
  };

  const focusAccountContent = (kind) => {
    const target = content.querySelector("[data-account-emergency-action]") || content.querySelector("[data-account-dialog-close], [data-account-manage-close], input, select, textarea, button, a");
    target?.focus({ preventScroll: true });
  };

  const replaceContent = (html) => {
    const parsed = new DOMParser().parseFromString(html, "text/html");
    const roots = parsed.querySelectorAll("[data-account-dialog-page]");
    if (roots.length !== 1 || !(roots[0] instanceof HTMLElement) || roots[0].getAttribute("data-account-dialog-overlay") !== "true") return false;
    build();
    content.replaceChildren(document.importNode(roots[0], true));
    const component = content.querySelector("[data-account-manage-page], [data-account-dialog-page]");
    const labelledBy = component?.getAttribute("aria-labelledby");
    const describedBy = component?.getAttribute("aria-describedby");
    if (labelledBy) dialog.setAttribute("aria-labelledby", labelledBy);
    else dialog.removeAttribute("aria-labelledby");
    if (describedBy) dialog.setAttribute("aria-describedby", describedBy);
    else dialog.removeAttribute("aria-describedby");
    bindContent();
    guard.initialize();
    return true;
  };

  const fallback = (href) => { window.location.assign(href); };

  const finishRedirect = (href) => {
    const destination = new URL(href || currentUrl(), window.location.href);
    parentRefreshFailed = false;
    hide(false);
    window.location.assign(destination.href);
  };

  const statusMessage = (html) => {
    const parsed = new DOMParser().parseFromString(html, "text/html");
    return parsed.querySelector("[data-account-dialog-page]")?.getAttribute("data-account-status-message")?.trim() || "";
  };

  const validationMessage = (html) => {
    const parsed = new DOMParser().parseFromString(html, "text/html");
    return parsed.querySelector("[data-account-dialog-page]")?.getAttribute("data-account-validation-message")?.trim() || "";
  };

  const responseToast = (html) => {
    const parsed = new DOMParser().parseFromString(html, "text/html");
    const toast = parsed.querySelector("[data-transient-toast]");
    const message = toast?.querySelector(".app-toast-copy")?.querySelector("span")?.textContent?.trim() || "";
    if (!message) return null;
    const type = [...(toast?.getAttribute("class") || "").matchAll(/(?:^|\s)app-toast-(success|error|warning|information)(?:\s|$)/g)][0]?.[1] || "success";
    return { message, type };
  };

  const isAccountsDirectoryResponse = (html) => {
    const parsed = new DOMParser().parseFromString(html, "text/html");
    return parsed.querySelector(".admin-accounts-page") instanceof HTMLElement;
  };

  const hideInlineValidation = () => {
    content.querySelectorAll(".validation-summary, .field-error").forEach((error) => error.classList.add("visually-hidden"));
  };

  const showResponseFeedback = (html) => {
    const error = validationMessage(html);
    if (error) {
      hideInlineValidation();
      window.showBingoToast?.(error, "error");
      return;
    }
    const message = statusMessage(html);
    if (message) window.showBingoToast?.(message, "success");
  };

  const bindEmergencyActions = () => {
    content.querySelectorAll("[data-account-emergency-action], [data-account-final-action]").forEach((trigger) => {
      if (!(trigger instanceof HTMLElement) || trigger.dataset.accountEmergencyBound === "true") return;
      trigger.dataset.accountEmergencyBound = "true";
      trigger.addEventListener("click", (event) => {
        event.preventDefault();
        guarded(() => openConfirmation(trigger));
      });
    });
  };

  const createFormUrl = (form) => {
    const url = new URL(form.action || currentUrl(), window.location.href);
    const params = new URLSearchParams();
    for (const [name, value] of new FormData(form)) if (typeof value === "string" && name !== "overlay") params.set(name, value);
    url.search = params.toString();
    return overlayUrl(url.href);
  };

  async function submitCreateScope(form) {
    const requestId = ++loadId;
    const url = createFormUrl(form);
    const finish = guard.begin(form);
    try {
      const response = await window.fetch(url, { credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } });
      if (!response.ok) throw new Error("Create scope request failed.");
      const html = await response.text();
      if (requestId !== loadId || !dialog?.open || !hasOverlay()) return;
      if (!replaceContent(html)) throw new Error("Create scope component was not returned.");
      history.replaceState(history.state, "", response.url || url);
    } catch {
      if (requestId === loadId) {
        form.removeAttribute("aria-busy");
        const selection = form.querySelector("select[name='eventId']");
        if (selection) selection.value = content.querySelector("input[name='Input.EventId']")?.value || "";
        guard.showFailure(adminText("adminAccountTeamsError"));
      }
    } finally { finish(); }
  }

  async function submitCreate(form, event) {
    event?.preventDefault();
    if (guard.pending) return;
    const requestId = ++loadId;
    const data = new FormData(form);
    const finish = guard.begin(form);
    data.set("Overlay", "true");
    try {
      const action = new URL(form.action || currentUrl(), window.location.href);
      action.searchParams.set("overlay", "1");
      const response = await window.fetch(action.href, {
        method: "POST",
        body: data,
        credentials: "same-origin",
        headers: { "X-Requested-With": "XMLHttpRequest" }
      });
      if (!response.ok) throw new Error("Create request failed.");
      const html = await response.text();
      if (requestId !== loadId || !dialog?.open || !hasOverlay()) return;
      const destination = new URL(response.url || action.href, window.location.href);
      if (isAccountsDirectoryResponse(html)) {
        const username = data.get("Input.Username");
        const fallbackMessage = typeof username === "string" && username.trim()
          ? adminText("adminAccountCreatedDisabled").replace("{0}", username.trim())
          : adminText("adminAccountCreated");
        const feedback = responseToast(html) || { message: fallbackMessage, type: "success" };
        // Refresh the originating filtered/paged directory before returning to it.
        const refreshed = await refreshDirectory();
        if (!refreshed) { finish(); guard.initialize(); guard.showFailure(adminText("adminAccountParentRefreshError")); return; }
        destination.href = parentUrl || destination.href;

        const state = { ...(history.state || {}) };
        delete state.accountManageOverlay;
        delete state.accountDialogReturnUrl;
        history.replaceState(state, "", destination.href);
        finish();
        hide(false);
        closing = false;
        window.setTimeout(() => window.showBingoToast?.(feedback.message, feedback.type), 0);
        return;
      }
      if (validationMessage(html)) { guard.showFailure(validationMessage(html)); return; }
      if (!replaceContent(html)) {
        throw new Error("Create overlay component was not returned.");
      }
      history.replaceState(history.state, "", response.url || action.href);
      focusAccountContent("create");
      showResponseFeedback(html);
    } catch {
      if (requestId === loadId) {
        form.removeAttribute("aria-busy");
        guard.showFailure(adminText("adminAccountCreateError"));
      }
    } finally { finish(); }
  }

  const bindCreateForms = () => {
    if (!content.querySelector("[data-account-create-page]")) return;
    const scopeForm = content.querySelector("form.admin-account-option-form");
    if (scopeForm instanceof HTMLFormElement && scopeForm.dataset.accountCreateBound !== "true") {
      scopeForm.dataset.accountCreateBound = "true";
      scopeForm.addEventListener("submit", (event) => {
        if (dialog?.open) {
          event.preventDefault();
          guarded(() => submitCreateScope(scopeForm), scopeForm);
        }
      });
      const selection = scopeForm.querySelector("select[name='eventId']");
      if (selection) selection.value = content.querySelector("input[name='Input.EventId']")?.value || selection.value;
      const loadedEvent = selection?.value;
      selection?.addEventListener("change", () => {
        const requestedEvent = selection.value;
        selection.value = loadedEvent;
        guarded(() => { selection.value = requestedEvent; submitCreateScope(scopeForm); }, scopeForm);
      });
    }
    const createForm = [...content.querySelectorAll("form")].find((form) => !form.classList.contains("admin-account-option-form"));
    if (createForm instanceof HTMLFormElement && createForm.dataset.accountCreateBound !== "true") {
      createForm.dataset.accountCreateBound = "true";
      createForm.addEventListener("submit", (event) => {
        if (dialog?.open) { event.preventDefault(); guarded(() => submitCreate(createForm), createForm); }
      });
    }
  };

  function bindContent() {
    if (!(content instanceof HTMLElement)) return;
    content.querySelectorAll("[data-account-dialog-close], [data-account-manage-close]").forEach((close) => {
      close.addEventListener("click", (event) => { event.preventDefault(); closeWithHistory(); });
    });
    content.querySelectorAll("[data-account-dialog-cancel], [data-account-confirmation-cancel]").forEach((cancel) => {
      cancel.addEventListener("click", (event) => {
        event.preventDefault();
        if (guard.pending || guard.cancelDiscard()) return;
        const confirmation = cancel.closest("details");
        if (confirmation instanceof HTMLDetailsElement) closeDetailsConfirmation(confirmation);
        else if (hasConfirmation()) closeConfirmation();
        else closeWithHistory();
      });
    });
    content.querySelectorAll("details.admin-account-action-confirmation").forEach(bindConfirmation);
    bindEmergencyActions();
    bindCreateForms();
    if (content.querySelector("[data-account-manage-page]")) content.querySelectorAll("form").forEach(form => {
      form.addEventListener("submit", event => { event.preventDefault(); guarded(() => submitManage(form), form); });
    });
  }

  function submitConfirmation(event) {
    event.preventDefault();
    guarded(() => submitManage(confirmationForm), confirmationForm);
  }

  async function submitManage(form) {
    if (guard.pending) return;
    if (!dialog?.open) { guard.initialize(); form.submit(); return; }
    const action = new URL(form.action || currentUrl(), window.location.href);
    action.searchParams.set("overlay", "1");
    const data = new FormData(form);
    data.set("overlay", "1");
    const finish = guard.begin(form);
    try {
      const response = await window.fetch(action.href, { method: "POST", body: data, credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } });
      if (!response.ok) throw new Error("Account request failed.");
      const html = await response.text();
      if (validationMessage(html)) {
        guard.showFailure(validationMessage(html));
        return;
      }
      if (!replaceContent(html)) {
        finish();
        finishRedirect(response.url || action.href);
        return;
      }
      history.replaceState(history.state, "", response.url || action.href);
      await refreshDirectory();
      finish();
      closeConfirmation(false);
      focusAccountContent("manage");
      showResponseFeedback(html);
      if (parentRefreshFailed) guard.showFailure(adminText("adminAccountParentRefreshError"));
    } catch {
      guard.showFailure(adminText("adminAccountActionError"));
    } finally { finish(); }
  }

  function openConfirmation(trigger) {
    closeConfirmation(false);
    build();
    confirmationOpener = trigger;
    const title = confirmationDialog.querySelector("#admin-account-confirmation-title");
    const support = confirmationDialog.querySelector("#admin-account-confirmation-support");
    const confirm = confirmationDialog.querySelector("[data-account-confirmation-submit]");
    const form = confirmationForm;
    if (!(title instanceof HTMLElement) || !(support instanceof HTMLElement) || !(confirm instanceof HTMLElement) || !(form instanceof HTMLFormElement)) return;
    title.textContent = trigger.dataset.accountConfirmationTitle || adminText("adminAccountConfirmTitle");
    support.textContent = trigger.dataset.accountConfirmationSupport || adminText("adminAccountConfirmSupport");
    confirm.textContent = trigger.dataset.accountConfirmationLabel || adminText("adminConfirm");
    confirm.className = trigger.dataset.accountConfirmationStyle === "danger" ? "action-danger-outline" : "admin-button-secondary";
    const action = new URL(currentUrl(), window.location.href);
    action.search = "";
    action.searchParams.set("handler", trigger.dataset.accountHandler || "");
    form.action = action.href;
    const actions = form.querySelector(".admin-account-confirmation-actions");
    form.replaceChildren();
    const token = content.querySelector("input[name='__RequestVerificationToken']");
    if (token instanceof HTMLInputElement) form.append(document.importNode(token, true));
    const overlay = document.createElement("input");
    overlay.type = "hidden";
    overlay.name = "overlay";
    overlay.value = hasOverlay() ? "1" : "0";
    form.append(overlay);
    if (trigger.dataset.accountConfirmationReason === "true") {
      const field = document.createElement("div");
      field.className = "admin-field";
      const label = document.createElement("label");
      label.htmlFor = "admin-account-confirmation-reason";
      label.textContent = trigger.dataset.accountConfirmationReasonLabel || adminText("adminReason");
      const reason = document.createElement("textarea");
      reason.id = label.htmlFor;
      reason.name = "Reason";
      reason.className = "form-control";
      reason.required = true;
      reason.maxLength = 500;
      field.append(label, reason);
      form.append(field);
    }
    form.append(actions);
    // Keep the confirmation beside its action, outside any enclosing form.
    const host = trigger.closest("form") || trigger;
    host.parentElement.append(confirmationDialog);
    confirmationDialog.hidden = false;
    trigger.hidden = true;
    guard.initialize();
    confirmationDialog.scrollIntoView({ block: "nearest", behavior: "instant" });
    window.setTimeout(() => confirmationDialog.querySelector("[data-account-confirmation-cancel]")?.focus({ preventScroll: true }), 0);
  }

  const hide = (restoreFocus = true, deferFocus = false) => {
    loadId++;
    closeConfirmation(false);
    if (dialog?.open) dialog.close();
    document.body.classList.remove("admin-route-dialog-open");
    content?.replaceChildren();
    const focusTarget = opener;
    const restore = () => {
      const target = focusTarget instanceof HTMLElement && document.body.contains(focusTarget) ? focusTarget : null;
      target?.focus({ preventScroll: true });
    };
    if (restoreFocus) {
      if (deferFocus) window.setTimeout(restore, 0);
      else restore();
    }
    opener = null;
    window.scrollTo?.(0, parentScroll);
    if (parentRefreshFailed) {
      if (new URL(parentUrl, window.location.href).href === currentUrl().href) window.location.reload();
      else window.location.replace(parentUrl);
    }
  };

  function closeWithHistory(afterClose) {
    if (guard.pending || guard.cancelDiscard()) return;
    if (guard.dirtyForms().length) { guard.confirmDiscard(() => closeNow(afterClose)); return; }
    closeNow(afterClose);
  }

  function closeNow(afterClose) {
    if (closing || guard.pending) return;
    closing = true;
    if (hasOverlay() && history.state?.accountManageOverlay) {
      if (afterClose) window.addEventListener("popstate", afterClose, { once: true });
      history.back();
    }
    else {
      const base = new URL(window.location.href);
      base.searchParams.delete("overlay");
      base.searchParams.delete("eventId");
      base.pathname = "/Admin/Accounts/Index";
      history.replaceState(history.state, "", base.href);
      hide();
      closing = false;
      afterClose?.();
    }
  }

  const findOpener = () => {
    if (!(directory instanceof HTMLElement)) return null;
    const url = currentUrl();
    return [...directory.querySelectorAll(triggerSelector)].find((trigger) => {
      try { return new URL(trigger.getAttribute("href"), window.location.href).pathname === url.pathname; }
      catch { return false; }
    });
  };

  const load = async (trigger, push) => {
    const href = push ? trigger.getAttribute("href") : currentUrl().href;
    if (!href) return;
    opener = trigger;
    if (push) { parentUrl = currentUrl().href; parentScroll = window.scrollY; parentRefreshFailed = false; }
    if (push) history.pushState({ ...(history.state || {}), accountManageOverlay: true, accountDialogReturnUrl: currentUrl().href }, "", overlayUrl(href));
    const requestId = ++loadId;
    try {
      const response = await window.fetch(overlayUrl(href), { credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } });
      if (!response.ok) throw new Error("Account management request failed.");
      const html = await response.text();
      if (requestId !== loadId || !hasOverlay()) return;
      if (!replaceContent(html)) throw new Error("Account dialog component was not returned.");
      if (!dialog.open) dialog.showModal();
      document.body.classList.add("admin-route-dialog-open");
      window.setTimeout(() => focusAccountContent(content.querySelector("[data-account-dialog-kind]")?.dataset.accountDialogKind), 0);
    } catch {
      if (requestId === loadId) {
        const destination = new URL(href, window.location.href);
        destination.searchParams.delete("overlay");
        fallback(destination.href);
      }
    }
  };

  const hydrateDirectory = (html) => {
    const parsed = new DOMParser().parseFromString(html, "text/html");
    const root = parsed.querySelector(".admin-accounts-page");
    if (!(root instanceof HTMLElement)) return false;
    const nextDirectory = document.importNode(root, true);
    if (directory instanceof HTMLElement && directory.parentElement === main) directory.replaceWith(nextDirectory);
    else main.replaceChildren(nextDirectory);
    directory = nextDirectory;
    if (opener) opener = findOpener() || directory.querySelector(triggerSelector);
    bindTriggers(directory);
    window.initializeAdminAccountSearch?.();
    document.dispatchEvent(new CustomEvent("bingo:content-updated", { detail: { section: directory } }));
    return true;
  };

  const refreshDirectory = async () => {
    try {
      const response = await window.fetch(parentUrl || "/Admin/Accounts/Index", { credentials: "same-origin" });
      if (!response.ok || !hydrateDirectory(await response.text())) throw new Error("Directory refresh failed.");
      parentRefreshFailed = false;
      window.scrollTo?.(0, parentScroll);
      return true;
    } catch { parentRefreshFailed = true; return false; }
  };

  const bootstrapDirectOverlay = async () => {
    if (directBootstrap || directory instanceof HTMLElement || !hasOverlay()) return;
    directBootstrap = true;
    try {
      const directUrl = currentUrl();
      const routeRoot = main.querySelector("[data-account-dialog-page]");
      const directoryUrl = new URL(history.state?.accountDialogReturnUrl || routeRoot?.getAttribute("data-account-directory-url") || "/Admin/Accounts/Index", directUrl.href);
      parentUrl = directoryUrl.href;
      parentScroll = window.scrollY;
      const [directoryResponse, overlayResponse] = await Promise.all([
        window.fetch(directoryUrl.href, { credentials: "same-origin" }),
        window.fetch(overlayUrl(directUrl.href), { credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } })
      ]);
      if (!directoryResponse.ok || !overlayResponse.ok) throw new Error("Accounts overlay request failed.");
      if (!hydrateDirectory(await directoryResponse.text())) throw new Error("Accounts directory could not be loaded.");
      const trigger = findOpener();
      if (!(trigger instanceof HTMLAnchorElement)) throw new Error("Account management link could not be resolved.");
      opener = trigger;
      const baseUrl = new URL(directoryUrl.href);
      history.replaceState({ ...(history.state || {}), accountDialogBase: true }, "", baseUrl.href);
      history.pushState({ ...(history.state || {}), accountManageOverlay: true, accountDialogReturnUrl: baseUrl.href }, "", directUrl.href);
      if (!replaceContent(await overlayResponse.text())) throw new Error("Accounts overlay component was not returned.");
      if (!dialog.open) dialog.showModal();
      document.body.classList.add("admin-route-dialog-open");
      window.setTimeout(() => focusAccountContent(content.querySelector("[data-account-dialog-kind]")?.dataset.accountDialogKind), 0);
    } catch {
      const destination = currentUrl();
      destination.searchParams.delete("overlay");
      fallback(destination.href);
    } finally {
      directBootstrap = false;
    }
  };

  const sync = () => {
    if (restoringHistory && hasOverlay()) { restoringHistory = false; return; }
    if (!hasOverlay() && dialog?.open && !closing && (guard.pending || guard.dirtyForms().length)) {
      restoringHistory = true;
      history.forward();
      if (!guard.pending) guard.confirmDiscard(closeNow);
      return;
    }
    if (hasOverlay()) {
      if (!dialog?.open) {
        const trigger = findOpener();
        if (trigger instanceof HTMLAnchorElement) load(trigger, false);
        else if (!(directory instanceof HTMLElement)) bootstrapDirectOverlay();
      }
    } else if (dialog?.open) {
      hide(true, true);
    }
    closing = false;
  };

  const bindTriggers = (root) => {
    root.querySelectorAll(triggerSelector).forEach((trigger) => {
      if (!(trigger instanceof HTMLAnchorElement) || trigger.dataset.accountManageBound === "true") return;
      trigger.dataset.accountManageBound = "true";
      trigger.addEventListener("click", (event) => {
          event.preventDefault();
        load(trigger, true);
      });
    });
  };

  const standalone = main.querySelector("[data-account-dialog-page]");
  if (!hasOverlay() && standalone instanceof HTMLElement) {
    content = standalone;
    bindEmergencyActions();
    content.querySelectorAll("details.admin-account-action-confirmation").forEach(bindConfirmation);
    content.querySelectorAll("[data-account-confirmation-cancel]").forEach(cancel => cancel.addEventListener("click", event => { event.preventDefault(); closeDetailsConfirmation(cancel.closest("details")); }));
    content.querySelectorAll("form").forEach(form => {
      const selection = form.classList.contains("admin-account-option-form") ? form.querySelector("select[name='eventId']") : null;
      if (selection) selection.value = content.querySelector("input[name='Input.EventId']")?.value || selection.value;
      const loadedEvent = selection?.value;
      form.addEventListener("submit", event => {
        if (!selection) { guard.initialize(); return; }
        event.preventDefault();
        const requestedEvent = selection.value;
        selection.value = loadedEvent;
        guarded(() => {
          selection.value = requestedEvent;
          guard.initialize();
          form.submit();
        }, form);
      });
    });
    guard.initialize();
  }
  if (directory instanceof HTMLElement) bindTriggers(directory);
  document.addEventListener("bingo:account-directory-updated", (event) => {
    if (event.detail?.section instanceof HTMLElement) bindTriggers(event.detail.section);
  });

  window.addEventListener("popstate", sync);
  window.addEventListener("beforeunload", event => {
    if (guard.pending || guard.dirtyForms().length) { event.preventDefault(); event.returnValue = ""; }
  });
  if (hasOverlay()) sync();
})();

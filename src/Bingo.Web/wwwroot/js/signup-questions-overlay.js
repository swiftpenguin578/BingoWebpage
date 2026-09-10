(() => {
  "use strict";

  const dialog = document.querySelector("[data-signup-questions-dialog]");
  const content = dialog?.querySelector("[data-signup-questions-content]");
  const triggerSelector = "[data-signup-questions-trigger='true']";
  if (!(dialog instanceof HTMLDialogElement) || !(content instanceof HTMLElement)) return;

  let opener = null;
  let closing = false;
  let loadId = 0;
  let restoringHistory = false;
  let parentRefreshFailed = false;
  const currentEditor = () => content.querySelector("[data-signup-questions-editor]") || document.querySelector("[data-signup-questions-editor]");
  const guard = window.createAdminEditorGuard({ editor: currentEditor, prefix: "signup-questions", closeConfirmation: () => closeOpenConfirmation(), saveError: () => document.body.dataset.signupQuestionsSaveError });
  const { dirtyForms, cancelDiscard, confirmDiscard, showFailure } = guard;

  const overlayUrl = () => new URL(window.location.href);
  const hasOverlay = () => overlayUrl().searchParams.get("signupQuestions") === "1";
  const editorUrl = () => document.querySelector(triggerSelector)?.href;
  const editorRequestUrl = (source) => {
    const url = new URL(source, window.location.href);
    url.searchParams.set("overlay", "1");
    return url.href;
  };
  const closeOpenConfirmation = () => {
    const confirmation = currentEditor()?.querySelector("details.signup-question-remove[open]");
    if (!(confirmation instanceof HTMLDetailsElement)) return false;
    confirmation.removeAttribute("open");
    confirmation.open = false;
    confirmation.querySelector("summary")?.focus({ preventScroll: true });
    return true;
  };

  const bindTriggers = () => {
    document.querySelectorAll(triggerSelector).forEach((trigger) => {
      if (trigger.dataset.signupQuestionsTriggerReady === "true") return;
      trigger.dataset.signupQuestionsTriggerReady = "true";
      trigger.addEventListener("click", (event) => {
        event.preventDefault();
        opener = trigger;
        show(true);
      });
    });
    if (!(opener instanceof HTMLElement) || !document.body.contains(opener)) opener = document.querySelector(triggerSelector);
  };

  const initializeEditor = (editor) => {
    editor.querySelectorAll("details.signup-question-edit > summary[aria-controls]").forEach((summary) => {
      if (summary.dataset.signupQuestionsEditReady === "true") return;
      summary.dataset.signupQuestionsEditReady = "true";
      const details = summary.closest("details");
      const form = document.getElementById(summary.getAttribute("aria-controls"));
      if (!(details instanceof HTMLDetailsElement) || !(form instanceof HTMLFormElement)) return;
      const syncEditForm = () => { form.hidden = !details.open; };
      details.addEventListener("toggle", syncEditForm);
      syncEditForm();
    });
    editor.querySelectorAll("[data-signup-question-cancel-removal]").forEach((button) => {
      if (button.dataset.signupQuestionsCancelReady === "true") return;
      button.dataset.signupQuestionsCancelReady = "true";
      button.addEventListener("click", closeOpenConfirmation);
    });
    editor.querySelectorAll("details.signup-question-remove").forEach((confirmation) => {
      if (confirmation.dataset.signupQuestionsConfirmationReady === "true") return;
      confirmation.dataset.signupQuestionsConfirmationReady = "true";
      confirmation.addEventListener("toggle", () => {
        if (!confirmation.open) return;
        editor.querySelectorAll("details.signup-question-remove[open]").forEach((other) => { if (other !== confirmation) other.open = false; });
        confirmation.querySelector("[data-signup-question-cancel-removal]")?.focus({ preventScroll: true });
      });
      confirmation.addEventListener("keydown", (event) => {
        if (event.key !== "Escape" || !confirmation.open) return;
        event.preventDefault();
        event.stopPropagation();
        closeOpenConfirmation();
      });
    });
    editor.querySelectorAll("form").forEach((form) => {
      if (form.dataset.signupQuestionsFormReady === "true") return;
      form.dataset.signupQuestionsFormReady = "true";
      form.addEventListener("submit", (event) => {
        if (event.defaultPrevented) return;
        event.preventDefault();
        if (guard.pending) return;
        const submit = () => submitForm(form, event.submitter);
        if (dirtyForms(form).length) confirmDiscard(submit);
        else submit();
      });
    });

    editor.querySelectorAll("[data-edit-question-type]").forEach((editType) => {
      if (editType.dataset.interactionsReady === "true") return;
      const editForm = editType.closest("form");
      const editRole = editForm?.querySelector("[data-edit-account-role]");
      const editOptions = editForm?.querySelector("[data-edit-choice-options]");
      if (!(editType instanceof HTMLSelectElement) || !(editRole instanceof HTMLElement) || !(editOptions instanceof HTMLElement)) return;
      editType.dataset.interactionsReady = "true";
      const updateEdit = () => { editRole.hidden = editType.value !== "Account"; editOptions.hidden = editType.value !== "SingleChoice"; };
      editType.addEventListener("change", updateEdit);
      updateEdit();
    });

    const type = editor.querySelector("[data-question-type]");
    const options = editor.querySelector("[data-choice-options]");
    const role = editor.querySelector("[data-account-role]");
    const required = editor.querySelector("[data-required-field]");
    if (type instanceof HTMLSelectElement && options instanceof HTMLElement && role instanceof HTMLElement) {
      const update = () => {
        options.hidden = type.value !== "SingleChoice";
        role.hidden = type.value !== "Account";
        if (required instanceof HTMLElement) {
          required.hidden = type.value === "Account";
          const input = required.querySelector("input");
          if (input instanceof HTMLInputElement && type.value === "Account") input.checked = false;
        }
      };
      type.addEventListener("change", update);
      update();
    }
    const codeToggle = editor.querySelector("[data-signup-code-toggle]");
    const codeControl = editor.querySelector("[data-signup-code-control]");
    const codeInput = editor.querySelector("[data-signup-code-input]");
    if (codeToggle instanceof HTMLInputElement && codeControl instanceof HTMLElement && codeInput instanceof HTMLInputElement) {
      const updateCode = () => {
        codeControl.hidden = !codeToggle.checked;
        codeInput.required = codeToggle.checked && codeInput.dataset.hasSignupCode !== "true";
      };
      codeToggle.addEventListener("change", updateCode);
      updateCode();
    }
    guard.initialize();
  };

  const replaceEditor = (html, standaloneEditor = null) => {
    const parsed = new DOMParser().parseFromString(html, "text/html");
    const editor = parsed.querySelector("[data-signup-questions-editor]");
    if (!(editor instanceof HTMLElement)) throw new Error(document.body.dataset.signupQuestionsEditorError || "");
    const questionCount = editor.dataset.signupQuestionCount;
    const questionSummary = document.querySelector("[data-signup-question-summary]");
    const questionSummaryTemplate = questionSummary?.dataset.signupQuestionSummary;
    if (questionCount !== undefined && questionSummary instanceof HTMLElement && questionSummaryTemplate) questionSummary.textContent = questionSummaryTemplate.replace("{0}", questionCount);
    const notice = parsed.querySelector("#app-notice-region");
    const noticeRegion = document.querySelector("#app-notice-region");
    if (notice instanceof HTMLElement && noticeRegion instanceof HTMLElement) {
      noticeRegion.replaceChildren(...Array.from(notice.childNodes, (node) => document.importNode(node, true)));
      window.initializeTransientToastLayer?.();
    }
    const replacement = document.importNode(editor, true);
    if (standaloneEditor) standaloneEditor.replaceWith(replacement);
    else content.replaceChildren(replacement);
    const currentEditor = replacement;
    const labelledBy = currentEditor?.getAttribute("aria-labelledby");
    const describedBy = currentEditor?.getAttribute("aria-describedby");
    if (labelledBy) dialog.setAttribute("aria-labelledby", labelledBy);
    else dialog.removeAttribute("aria-labelledby");
    if (describedBy) dialog.setAttribute("aria-describedby", describedBy);
    else dialog.removeAttribute("aria-describedby");
    if (!standaloneEditor) currentEditor?.querySelector("[data-signup-questions-close]")?.addEventListener("click", closeWithHistory);
    if (currentEditor instanceof HTMLElement) initializeEditor(currentEditor);
  };

  const refreshParticipants = async () => {
    const page = document.querySelector(".event-participants-page");
    if (!page) return;
    parentRefreshFailed = true;
    const url = overlayUrl();
    url.searchParams.delete("signupQuestions");
    const response = await window.fetch(url.href, { credentials: "same-origin" });
    if (!response.ok) throw new Error();
    const replacement = new DOMParser().parseFromString(await response.text(), "text/html").querySelector(".event-participants-page");
    if (!(replacement instanceof HTMLElement)) throw new Error();
    const scroll = { left: window.scrollX, top: window.scrollY };
    const currentUrl = overlayUrl();
    const state = history.state;
    document.dispatchEvent(new CustomEvent("bingo:content-will-update", { detail: { selectors: [".event-participants-page"] } }));
    page.replaceWith(document.importNode(replacement, true));
    document.dispatchEvent(new CustomEvent("bingo:content-updated", { detail: { selectors: [".event-participants-page"] } }));
    history.replaceState(state, "", currentUrl);
    window.scrollTo(scroll);
    parentRefreshFailed = false;
  };

  const submitForm = async (form, submitter) => {
    if (guard.pending) return;
    const standaloneEditor = dialog.open ? null : currentEditor();
    const requestId = ++loadId;
    const options = { method: (form.method || "post").toUpperCase(), credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } };
    options.body = new FormData(form);
    if (submitter?.name) options.body.append(submitter.name, submitter.value);
    const finish = guard.begin(form);
    try {
      const response = await window.fetch(form.action || editorUrl(), options);
      if (!response.ok) throw new Error();
      const html = await response.text();
      if (requestId !== loadId) return;
      const parsed = new DOMParser().parseFromString(html, "text/html");
      if (!parsed.querySelector("[data-signup-questions-editor]")) throw new Error();
      const errors = Array.from(parsed.querySelectorAll(".field-validation-error, .validation-summary-errors, .app-toast-error .app-toast-copy span, .app-toast-warning .app-toast-copy span")).map((error) => error.textContent.trim()).filter(Boolean);
      if (errors.length || parsed.querySelector("[data-signup-questions-invalid='true']")) { showFailure(errors.join(" ")); return; }
      replaceEditor(html, standaloneEditor);
      try { await refreshParticipants(); }
        catch { showFailure(document.body.dataset.adminParentRefreshError); return; }
      currentEditor()?.querySelector("[data-signup-questions-close]")?.focus({ preventScroll: true });
    } catch {
      showFailure();
    } finally {
      finish();
    }
  };

  const prepareEditor = async () => {
    const source = editorUrl();
    if (!source) return false;
    const requestId = ++loadId;
    content.setAttribute("aria-busy", "true");
    try {
      const response = await window.fetch(editorRequestUrl(source), { credentials: "same-origin" });
      if (!response.ok) throw new Error(document.body.dataset.signupQuestionsRequestError || "");
      const html = await response.text();
      if (requestId !== loadId || !hasOverlay()) return false;
      replaceEditor(html);
      return true;
    } catch {
      if (requestId !== loadId || !hasOverlay()) return false;
      if (requestId === loadId) {
        const message = Object.assign(document.createElement("p"), { textContent: document.body.dataset.signupQuestionsLoadError || "", role: "alert" });
        const retry = Object.assign(document.createElement("button"), { type: "button", className: "admin-button-secondary", textContent: document.body.dataset.signupQuestionsRetry || "" });
        const close = Object.assign(document.createElement("button"), { type: "button", className: "admin-button-secondary", textContent: document.body.dataset.signupQuestionsClose || "" });
        retry.addEventListener("click", prepareAndShow);
        close.addEventListener("click", closeWithHistory);
        close.setAttribute("data-signup-questions-close", "");
        const panel = Object.assign(document.createElement("section"), { className: "signup-question-page signup-question-dialog" });
        panel.append(message, retry, close);
        content.replaceChildren(panel);
      }
      return true;
    } finally {
      if (requestId === loadId) content.removeAttribute("aria-busy");
    }
  };

  const prepareAndShow = async () => {
    const requestId = loadId + 1;
    const ready = await prepareEditor();
    if (!ready || requestId !== loadId || !hasOverlay()) return;
    if (!dialog.open) dialog.showModal();
    document.body.classList.add("admin-route-dialog-open");
    window.setTimeout(() => content.querySelector("[data-signup-questions-close]")?.focus({ preventScroll: true }), 0);
  };

  const show = (push) => {
    if (guard.pending) return;
    if (!(opener instanceof HTMLElement) || !document.body.contains(opener)) {
      opener = document.querySelector(triggerSelector);
    }
    if (push) {
      const url = overlayUrl();
      url.searchParams.set("signupQuestions", "1");
      history.pushState({ ...(history.state || {}), signupQuestionsOverlay: true }, "", url);
    }
    prepareAndShow();
  };

  const hide = (restoreFocus = true, deferFocus = false) => {
    loadId++;
    if (dialog.open) dialog.close();
    document.body.classList.remove("admin-route-dialog-open");
    content.replaceChildren();
    content.removeAttribute("aria-busy");
    const focusTarget = opener;
    const restore = () => {
      const target = focusTarget instanceof HTMLElement && document.body.contains(focusTarget) ? focusTarget : document.querySelector(triggerSelector);
      if (target instanceof HTMLElement) target.focus({ preventScroll: true });
    };
    if (restoreFocus) {
      if (deferFocus) window.setTimeout(restore, 0);
      else restore();
    }
    opener = null;
    if (parentRefreshFailed) {
      const url = overlayUrl();
      url.searchParams.delete("signupQuestions");
      if (url.href === overlayUrl().href) window.location.reload();
      else window.location.replace(url.href);
    }
  };

  const closeWithHistory = () => {
    if (closing || guard.pending || !hasOverlay()) return;
    if (dirtyForms().length) { confirmDiscard(closeNow); return; }
    closeNow();
  };

  const closeNow = () => {
    if (closing || guard.pending) return;
    closing = true;
    if (hasOverlay()) history.back();
    else {
      const url = overlayUrl();
      url.searchParams.delete("signupQuestions");
      history.replaceState(history.state, "", url);
      hide();
      closing = false;
    }
  };

  const sync = () => {
    if (restoringHistory && hasOverlay()) { restoringHistory = false; return; }
    if (!hasOverlay() && dialog.open && !closing && (guard.pending || dirtyForms().length)) {
      restoringHistory = true;
      history.forward();
      if (!guard.pending) confirmDiscard(closeNow);
      return;
    }
    if (hasOverlay()) {
      if (!dialog.open) show(false);
    } else if (dialog.open) {
      hide(true, true);
    }
    closing = false;
  };

  const standalone = document.querySelector("[data-signup-questions-editor]");
  if (standalone instanceof HTMLElement) initializeEditor(standalone);
  window.addEventListener("beforeunload", (event) => {
    if (guard.pending || dirtyForms().length) { event.preventDefault(); event.returnValue = ""; }
  });
  bindTriggers();
  document.addEventListener("bingo:content-updated", bindTriggers);
  dialog.addEventListener("cancel", (event) => { event.preventDefault(); if (!guard.pending && !cancelDiscard() && !closeOpenConfirmation()) closeWithHistory(); });
  dialog.addEventListener("click", (event) => { if (event.target === dialog && !guard.pending && !cancelDiscard() && !closeOpenConfirmation()) closeWithHistory(); });
  window.addEventListener("popstate", sync);

  if (hasOverlay()) {
    const base = overlayUrl();
    base.searchParams.delete("signupQuestions");
    history.replaceState({ ...(history.state || {}), signupQuestionsBase: true }, "", base);
    const reopened = new URL(window.location.href);
    reopened.searchParams.set("signupQuestions", "1");
    history.pushState({ ...(history.state || {}), signupQuestionsOverlay: true }, "", reopened);
    show(false);
  }
})();

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
    if (!window.adminConfirmation?.active) return false;
    window.adminConfirmation.cancel();
    return true;
  };

  const destructiveConfirmationDetails = form => ({
    title: form.dataset.signupQuestionConfirmationTitle || "",
    description: [form.dataset.signupQuestionConfirmationDescription, form.dataset.signupQuestionConfirmationImpact]
      .filter(Boolean)
      .join(" "),
    actionLabel: form.dataset.signupQuestionConfirmationAction || ""
  });

  const confirmationQuestionId = form => form?.querySelector("input[name='questionId']")?.value || "";
  const findConfirmationForm = (root, questionId) => {
    if (!root?.querySelectorAll || !questionId) return null;
    return Array.from(root.querySelectorAll("form[data-signup-question-confirmation='true']"))
      .find(candidate => confirmationQuestionId(candidate) === questionId) || null;
  };
  const refreshDestructiveConfirmation = (form, refreshedForm) => {
    ["expectedAnswerCount", "expectedEventRegistrationReleaseCount", "expectedQuestionVersion"].forEach(name => {
      const source = refreshedForm.querySelector(`input[name='${name}']`);
      const target = form.querySelector(`input[name='${name}']`);
      if (source instanceof HTMLInputElement && target instanceof HTMLInputElement) target.value = source.value;
    });
    ["signupQuestionConfirmationTitle", "signupQuestionConfirmationDescription", "signupQuestionConfirmationImpact", "signupQuestionConfirmationAction"].forEach(name => {
      if (refreshedForm.dataset[name] !== undefined) form.dataset[name] = refreshedForm.dataset[name];
    });
    const details = destructiveConfirmationDetails(form);
    const confirmation = document.querySelector("[data-admin-confirmation]");
    if (confirmation instanceof HTMLElement) {
      const title = confirmation.querySelector("#admin-confirmation-title");
      const description = confirmation.querySelector("#admin-confirmation-description");
      const action = confirmation.querySelector("[data-admin-confirmation-action]");
      if (title instanceof HTMLElement) title.textContent = details.title;
      if (description instanceof HTMLElement) description.textContent = details.description;
      if (action instanceof HTMLElement) action.textContent = details.actionLabel;
    }
  };

  const openDestructiveConfirmation = (form, submitter) => {
    if (!window.adminConfirmation) {
      showFailure(document.body.dataset.signupQuestionsConfirmationError || "");
      return;
    }
    const details = destructiveConfirmationDetails(form);
    const opener = submitter instanceof HTMLElement ? submitter : form.querySelector("button[type='submit']");
    try {
      window.adminConfirmation.open({
        title: details.title,
        description: details.description,
        actionLabel: details.actionLabel,
        danger: true,
        opener,
        onConfirm: () => submitForm(form, submitter)
      }).catch(() => { showFailure(document.body.dataset.signupQuestionsConfirmationError || ""); });
    } catch {
      showFailure(document.body.dataset.signupQuestionsConfirmationError || "");
    }
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
    editor.querySelectorAll("form").forEach((form) => {
      if (form.dataset.signupQuestionsFormReady === "true") return;
      form.dataset.signupQuestionsFormReady = "true";
      form.addEventListener("submit", (event) => {
        if (event.defaultPrevented) return;
        event.preventDefault();
        if (guard.pending) return;
        const submit = () => {
          if (form.dataset.signupQuestionConfirmation === "true") openDestructiveConfirmation(form, event.submitter);
          else void submitForm(form, event.submitter);
        };
        if (dirtyForms(form).length) confirmDiscard(submit);
        else submit();
      });
    });

    editor.querySelectorAll("[data-edit-question-type]").forEach((editType) => {
      if (editType.dataset.interactionsReady === "true") return;
      const editForm = editType.closest("form");
      const editOptions = editForm?.querySelector("[data-edit-choice-options]");
      if (!(editType instanceof HTMLSelectElement) || !(editOptions instanceof HTMLElement)) return;
      editType.dataset.interactionsReady = "true";
      const updateEdit = () => { editOptions.hidden = editType.value !== "SingleChoice"; };
      editType.addEventListener("change", updateEdit);
      updateEdit();
    });

    const type = editor.querySelector("[data-question-type]");
    const options = editor.querySelector("[data-choice-options]");
    const required = editor.querySelector("[data-required-field]");
    if (type instanceof HTMLSelectElement && options instanceof HTMLElement) {
      const update = () => {
        options.hidden = type.value !== "SingleChoice";
        if (required instanceof HTMLElement) {
          required.hidden = false;
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
    if (guard.pending) return { succeeded: false, message: "" };
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
      if (requestId !== loadId) return { succeeded: false, message: "" };
      const parsed = new DOMParser().parseFromString(html, "text/html");
      if (!parsed.querySelector("[data-signup-questions-editor]")) throw new Error();
      const errors = Array.from(parsed.querySelectorAll(".field-validation-error, .validation-summary-errors, .app-toast-error .app-toast-copy span, .app-toast-warning .app-toast-copy span")).map((error) => error.textContent.trim()).filter(Boolean);
      const staleImpactMessage = errors.find(error => /reload this question before confirming/i.test(error));
      if (staleImpactMessage) {
        const refreshedForm = findConfirmationForm(parsed, confirmationQuestionId(form));
        if (refreshedForm) refreshDestructiveConfirmation(form, refreshedForm);
      }
      if (errors.length || parsed.querySelector("[data-signup-questions-invalid='true']")) {
        const message = errors.join(" ");
        showFailure(message);
        return { succeeded: false, message };
      }
      replaceEditor(html, standaloneEditor);
      try { await refreshParticipants(); }
        catch {
          const message = document.body.dataset.adminParentRefreshError || "";
          showFailure(message);
          return { succeeded: false, message };
        }
      currentEditor()?.querySelector("[data-signup-questions-close]")?.focus({ preventScroll: true });
      return true;
    } catch {
      const message = document.body.dataset.signupQuestionsSaveError || "";
      showFailure(message);
      return { succeeded: false, message };
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
    if (restoringHistory && hasOverlay()) {
      restoringHistory = false;
      if (!guard.pending && dirtyForms().length) confirmDiscard(closeNow);
      return;
    }
    if (!hasOverlay() && dialog.open && !closing && (guard.pending || dirtyForms().length)) {
      restoringHistory = true;
      history.forward();
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
  window.watchAdminUnsavedChanges(() => dirtyForms().length > 0);
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

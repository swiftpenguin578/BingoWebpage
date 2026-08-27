(() => {
  "use strict";

  const dialog = document.querySelector("[data-signup-questions-dialog]");
  const content = dialog?.querySelector("[data-signup-questions-content]");
  const triggerSelector = "[data-signup-questions-trigger='true']";
  if (!(dialog instanceof HTMLDialogElement) || !(content instanceof HTMLElement)) return;

  let opener = null;
  let closing = false;
  let loadId = 0;

  const canEnhance = () => window.innerWidth > 900;
  const overlayUrl = () => new URL(window.location.href);
  const hasOverlay = () => overlayUrl().searchParams.get("signupQuestions") === "1";
  const editorUrl = () => document.querySelector(triggerSelector)?.href;
  const editorRequestUrl = (source) => {
    const url = new URL(source, window.location.href);
    url.searchParams.set("overlay", "1");
    return url.href;
  };
  const closeOpenConfirmation = () => {
    const confirmation = content.querySelector("details.signup-question-remove[open]");
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
        if (!canEnhance()) return;
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
        if (!event.defaultPrevented && hasOverlay()) {
          event.preventDefault();
          submitForm(form);
        }
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
    if (!(type instanceof HTMLSelectElement) || !(options instanceof HTMLElement) || !(role instanceof HTMLElement)) return;
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
  };

  const replaceEditor = (html) => {
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
    content.replaceChildren(document.importNode(editor, true));
    const currentEditor = content.querySelector("[data-signup-questions-editor]");
    const labelledBy = currentEditor?.getAttribute("aria-labelledby");
    const describedBy = currentEditor?.getAttribute("aria-describedby");
    if (labelledBy) dialog.setAttribute("aria-labelledby", labelledBy);
    else dialog.removeAttribute("aria-labelledby");
    if (describedBy) dialog.setAttribute("aria-describedby", describedBy);
    else dialog.removeAttribute("aria-describedby");
    currentEditor?.querySelector("[data-signup-questions-close]")?.addEventListener("click", closeWithHistory);
    if (currentEditor instanceof HTMLElement) initializeEditor(currentEditor);
  };

  const submitForm = async (form) => {
    const requestId = ++loadId;
    form.setAttribute("aria-busy", "true");
    try {
      const method = (form.method || "post").toUpperCase();
      const options = { method, credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } };
      if (method !== "GET") options.body = new FormData(form);
      const response = await window.fetch(form.action || editorUrl(), options);
      if (!response.ok) throw new Error(document.body.dataset.signupQuestionsRequestError || "");
      const html = await response.text();
      if (requestId !== loadId || !dialog.open) return;
      replaceEditor(html);
    } catch {
      if (requestId === loadId && dialog.open) {
        content.replaceChildren(Object.assign(document.createElement("p"), { textContent: document.body.dataset.signupQuestionsLoadError || "", role: "alert" }));
      }
    } finally {
      form.removeAttribute("aria-busy");
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
      if (requestId !== loadId || !hasOverlay() || !canEnhance()) return false;
      replaceEditor(html);
      return true;
    } catch {
      if (requestId !== loadId || !hasOverlay() || !canEnhance()) return false;
      if (requestId === loadId) {
        content.replaceChildren(Object.assign(document.createElement("p"), { textContent: document.body.dataset.signupQuestionsLoadError || "", role: "alert" }));
      }
      return true;
    } finally {
      if (requestId === loadId) content.removeAttribute("aria-busy");
    }
  };

  const prepareAndShow = async () => {
    const requestId = loadId + 1;
    const ready = await prepareEditor();
    if (!ready || requestId !== loadId || !hasOverlay() || !canEnhance()) return;
    if (!dialog.open) dialog.showModal();
    document.body.classList.add("admin-route-dialog-open");
    window.setTimeout(() => content.querySelector("[data-signup-questions-close]")?.focus({ preventScroll: true }), 0);
  };

  const show = (push) => {
    if (!canEnhance()) return;
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
  };

  const closeWithHistory = () => {
    if (closing) return;
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
    if (!canEnhance()) {
      if (hasOverlay()) {
        const url = overlayUrl();
        url.searchParams.delete("signupQuestions");
        history.replaceState(history.state, "", url);
      }
      if (dialog.open) hide();
      closing = false;
      return;
    }
    if (hasOverlay()) {
      if (!dialog.open) show(false);
    } else if (dialog.open) {
      hide(true, true);
    }
    closing = false;
  };

  bindTriggers();
  document.addEventListener("bingo:content-updated", bindTriggers);
  dialog.addEventListener("cancel", (event) => { event.preventDefault(); if (!closeOpenConfirmation()) closeWithHistory(); });
  dialog.addEventListener("click", (event) => { if (event.target === dialog && !closeOpenConfirmation()) closeWithHistory(); });
  window.addEventListener("popstate", sync);

  if (hasOverlay() && canEnhance()) {
    const base = overlayUrl();
    base.searchParams.delete("signupQuestions");
    history.replaceState({ ...(history.state || {}), signupQuestionsBase: true }, "", base);
    const reopened = new URL(window.location.href);
    reopened.searchParams.set("signupQuestions", "1");
    history.pushState({ ...(history.state || {}), signupQuestionsOverlay: true }, "", reopened);
    show(false);
  } else if (hasOverlay()) {
    const base = overlayUrl();
    base.searchParams.delete("signupQuestions");
    history.replaceState(history.state, "", base);
  }

  window.addEventListener("resize", sync);
})();

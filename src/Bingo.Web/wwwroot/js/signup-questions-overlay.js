(() => {
  "use strict";

  const dialog = document.querySelector("[data-signup-questions-dialog]");
  const frame = dialog?.querySelector("[data-signup-questions-frame]");
  const closeButton = dialog?.querySelector("[data-signup-questions-close]");
  const triggerSelector = "[data-signup-questions-trigger='true']";
  if (!(dialog instanceof HTMLDialogElement) || !(frame instanceof HTMLIFrameElement)) return;

  let opener = null;
  let closing = false;

  const overlayUrl = () => new URL(window.location.href);
  const hasOverlay = () => overlayUrl().searchParams.get("signupQuestions") === "1";
  const editorUrl = () => document.querySelector(triggerSelector)?.href;

  const show = (push) => {
    const source = editorUrl();
    if (!source) return;
    if (push) {
      const url = overlayUrl();
      url.searchParams.set("signupQuestions", "1");
      history.pushState({ ...(history.state || {}), signupQuestionsOverlay: true }, "", url);
    }
    frame.src = `${source}${source.includes("?") ? "&" : "?"}overlay=1`;
    if (!dialog.open) dialog.showModal();
    document.body.classList.add("admin-route-dialog-open");
    window.setTimeout(() => closeButton?.focus({ preventScroll: true }), 0);
  };

  const hide = () => {
    if (dialog.open) dialog.close();
    document.body.classList.remove("admin-route-dialog-open");
    frame.removeAttribute("src");
    const target = document.activeElement;
    if (opener instanceof HTMLElement) opener.focus({ preventScroll: true });
    else if (target instanceof HTMLElement) target.focus({ preventScroll: true });
    opener = null;
  };

  const closeWithHistory = () => {
    if (closing) return;
    closing = true;
    if (hasOverlay() && history.state?.signupQuestionsOverlay) history.back();
    else {
      const url = overlayUrl();
      url.searchParams.delete("signupQuestions");
      history.replaceState(history.state, "", url);
      hide();
      closing = false;
    }
  };

  const sync = () => {
    if (hasOverlay()) {
      if (!history.state?.signupQuestionsOverlay) history.replaceState({ ...(history.state || {}), signupQuestionsBase: true }, "");
      show(false);
    } else if (dialog.open) {
      hide();
    }
    closing = false;
  };

  document.querySelectorAll(triggerSelector).forEach((trigger) => trigger.addEventListener("click", (event) => {
    event.preventDefault();
    opener = trigger;
    show(true);
  }));
  closeButton.addEventListener("click", closeWithHistory);
  dialog.addEventListener("cancel", (event) => { event.preventDefault(); closeWithHistory(); });
  dialog.addEventListener("click", (event) => { if (event.target === dialog) closeWithHistory(); });
  window.addEventListener("popstate", sync);
  window.addEventListener("message", (event) => {
    if (event.origin !== window.location.origin || event.source !== frame.contentWindow) return;
    if (event.data?.type === "signup-questions-close") {
      closeWithHistory();
      return;
    }
    if (event.data?.type !== "signup-questions-saved") return;
    const message = typeof event.data.message === "string" ? event.data.message : "Signup questions saved.";
    sessionStorage.setItem("bingo:pending-toast", JSON.stringify({ message, type: "success" }));
    const url = overlayUrl();
    url.searchParams.delete("signupQuestions");
    history.replaceState(history.state, "", url);
    hide();
    window.location.reload();
  });

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

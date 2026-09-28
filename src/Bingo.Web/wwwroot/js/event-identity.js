(() => {
  "use strict";
  const root = document.querySelector("[data-identity-editor]");
  if (!(root instanceof HTMLElement)) return;

  let form = root.querySelector("form");
  let review = root.querySelector("[data-identity-timezone-preview]");
  let confirmButton = review?.querySelector("[data-identity-timezone-confirm]");
  if (!(form instanceof HTMLFormElement)) return;

  const guard = typeof window.createAdminEditorGuard === "function"
    ? window.createAdminEditorGuard({ editor: () => root, prefix: "identity-editor", saveError: () => root.dataset.identitySaveError || "" })
    : null;
  guard?.initialize(form);

  let previewProposalPending = review instanceof HTMLElement && confirmButton instanceof HTMLButtonElement;
  const hasDirtyChanges = () => previewProposalPending || Boolean(guard?.dirtyForms().length);
  const discardPendingProposal = () => {
    previewProposalPending = false;
    guard?.initialize();
  };
  const stopWatchingUnsaved = typeof window.watchAdminUnsavedChanges === "function"
    ? window.watchAdminUnsavedChanges(() => hasDirtyChanges())
    : null;
  void stopWatchingUnsaved;

  const hideInlineConfirmation = () => {
    if (!(review instanceof HTMLElement) || !(confirmButton instanceof HTMLButtonElement)) return;
    review.hidden = true;
    confirmButton.hidden = true;
    confirmButton.disabled = true;
  };

  const keepInlineConfirmationHidden = () => hideInlineConfirmation();

  const bindNavigationLinks = () => {
    root.querySelectorAll("[data-identity-cancel]").forEach(link => {
      if (!(link instanceof HTMLAnchorElement) || link.dataset.identityCancelReady === "true") return;
      link.dataset.identityCancelReady = "true";
      link.addEventListener("click", event => {
        if (event.defaultPrevented || !guard || !window.adminConfirmation || !hasDirtyChanges()) return;
        const destination = link.href;
        event.preventDefault();
        guard.confirmDiscard(() => {
          discardPendingProposal();
          window.location.assign(destination);
        });
      });
    });
  };

  bindNavigationLinks();

  let allowHistoryNavigation = false;
  if (guard && window.adminConfirmation && typeof window.history.pushState === "function") {
    const state = window.history.state;
    if (!state?.identityEditorSentinel) {
      const baseState = { ...(state || {}), identityEditorBase: true };
      window.history.replaceState(baseState, "", window.location.href);
      window.history.pushState({ ...baseState, identityEditorSentinel: true }, "", window.location.href);
    }

    window.addEventListener("popstate", event => {
      if (allowHistoryNavigation) {
        allowHistoryNavigation = false;
        return;
      }
      if (!event.state?.identityEditorBase) return;
      if (!hasDirtyChanges() || !window.adminConfirmation) {
        window.history.back();
        return;
      }

      window.history.pushState({ ...event.state, identityEditorSentinel: true }, "", window.location.href);
      guard.confirmDiscard(() => {
        discardPendingProposal();
        allowHistoryNavigation = true;
        window.history.go(-2);
      });
    });
  }

  const confirmationDetails = currentReview => {
    const rows = [...currentReview.querySelectorAll("[data-identity-timezone-row]")]
      .map(row => row.textContent.trim().replace(/\s+/g, " "));
    return {
      title: currentReview.dataset.title,
      actionLabel: currentReview.dataset.actionLabel,
      description: [currentReview.querySelector("[data-identity-timezone-consequence]")?.textContent?.trim(), ...rows]
        .filter(Boolean)
        .join(" ")
    };
  };

  const updateOpenConfirmation = currentReview => {
    if (!(currentReview instanceof HTMLElement)) return;
    const details = confirmationDetails(currentReview);
    const dialog = document.querySelector("[data-admin-confirmation]");
    dialog?.querySelector("#admin-confirmation-title")?.replaceChildren(document.createTextNode(details.title || ""));
    dialog?.querySelector("#admin-confirmation-description")?.replaceChildren(document.createTextNode(details.description));
    const action = dialog?.querySelector("[data-admin-confirmation-action]");
    if (action instanceof HTMLElement) action.textContent = details.actionLabel || "";
  };

  const replaceEditorFromResponse = (html, responseUrl) => {
    const parsed = new DOMParser().parseFromString(html, "text/html");
    const nextRoot = parsed.querySelector("[data-identity-editor]");
    const nextForm = nextRoot?.querySelector("form");
    const nextReview = nextRoot?.querySelector("[data-identity-timezone-preview]");
    const nextConfirmButton = nextReview?.querySelector("[data-identity-timezone-confirm]");
    if (!(nextRoot instanceof HTMLElement) || !(nextForm instanceof HTMLFormElement)
        || !(nextReview instanceof HTMLElement) || !(nextConfirmButton instanceof HTMLButtonElement)) {
      window.location.assign(responseUrl);
      return { navigated: true, message: "" };
    }

    root.dataset.identitySaveError = nextRoot.dataset.identitySaveError || root.dataset.identitySaveError || "";
    const importedRoot = document.importNode(nextRoot, true);
    const previousSave = root.querySelector("[data-identity-save]");
    const importedSave = importedRoot.querySelector("[data-identity-save]");
    if (previousSave instanceof HTMLElement && importedSave instanceof HTMLElement)
      importedSave.replaceWith(previousSave);
    root.replaceChildren(...importedRoot.childNodes);
    form = root.querySelector("form");
    review = root.querySelector("[data-identity-timezone-preview]");
    confirmButton = review?.querySelector("[data-identity-timezone-confirm]");
    if (!(form instanceof HTMLFormElement) || !(review instanceof HTMLElement) || !(confirmButton instanceof HTMLButtonElement)) {
      window.location.assign(responseUrl);
      return { navigated: true, message: "" };
    }

    guard?.initialize(form);
    previewProposalPending = true;
    bindNavigationLinks();
    hideInlineConfirmation();
    updateOpenConfirmation(review);
    return { navigated: false, message: root.querySelector(".validation-summary")?.textContent?.trim() || "" };
  };

  const submitConfirmation = async () => {
    if (!(form instanceof HTMLFormElement)) return { succeeded: false, message: root.dataset.identitySaveError || "" };
    const submittedForm = form;
    const data = new FormData(submittedForm);
    data.set("Input.ConfirmTimezoneChange", "true");
    const finish = guard?.begin(submittedForm);
    try {
      const response = await window.fetch(submittedForm.action || window.location.href, { method: "POST", body: data, credentials: "same-origin" });
      if (response.redirected) {
        window.location.assign(response.url);
        return true;
      }
      const html = await response.text();
      const result = replaceEditorFromResponse(html, response.url);
      if (result.navigated) return true;
      return { succeeded: false, message: result.message || root.dataset.identitySaveError || "" };
    } catch {
      return { succeeded: false, message: root.dataset.identitySaveError || "" };
    } finally {
      finish?.();
    }
  };

  if (!(review instanceof HTMLElement) || !(confirmButton instanceof HTMLButtonElement) || !window.adminConfirmation) return;

  hideInlineConfirmation();
  const details = confirmationDetails(review);
  window.adminConfirmation.open({
    title: details.title,
    description: details.description,
    actionLabel: details.actionLabel,
    opener: root.querySelector("[data-identity-save]") || confirmButton,
    onConfirm: submitConfirmation
  }).then(keepInlineConfirmationHidden);
})();

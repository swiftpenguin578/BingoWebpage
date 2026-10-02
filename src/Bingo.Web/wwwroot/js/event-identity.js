(() => {
  "use strict";
  // Client expectation, not a reconstruction of the server's eventual three-way merge.
  // Keep this session separate from the original draft used by AU08 conflict handling.
  window.createIdentityReadbackSession = (data, action, eventId) => {
    const fields = ["Name", "Description", "BuyInDescription", "Timezone"];
    // Match .NET String.Trim (including U+0085, excluding U+FEFF).
    const trim = value => value.replace(/^[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+|[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+$/g, "");
    const canonical = (field, value) => {
      const result = trim(value);
      return field === "Description" || field === "BuyInDescription" ? result || null : result;
    };
    const yes = key => String(data.get(key)).toLowerCase() === "true";
    const reviewed = yes("Input.HasReviewedValues");
    const baseline = yes("Input.HasBaseline");
    const expected = {};
    let complete = Boolean(eventId);
    for (const field of fields) {
      const get = (prefix, submitted = false) => {
        const value = data.get(`Input.${prefix}${field}`);
        if (typeof value !== "string") { complete = false; return null; }
        // Native multipart serialization sends CRLF, even when a textarea supplies LF.
        return canonical(field, submitted ? value.replace(/\r\n|\r|\n/g, "\r\n") : value);
      };
      const draft = get("", true);
      const original = baseline ? get("Original", true) : draft;
      // Retained/current values are observed state, not text we intend to write.
      const observed = reviewed ? get("Reviewed") : baseline ? get("Original") : draft;
      const choice = data.get(`Input.${field}Resolution`);
      if (choice && !["None", "KeepMine", "UseCurrent", "0", "1", "2"].includes(choice)) complete = false;
      if (["UseCurrent", "2"].includes(choice) && !reviewed) complete = false;
      expected[field[0].toLowerCase() + field.slice(1)] = draft === original
        || ["UseCurrent", "2"].includes(choice) ? observed : draft;
    }
    Object.freeze(expected);
    const url = new URL(action, window.location.href);
    url.searchParams.set("handler", "Current");
    const unknown = () => ({ state: "unknown", expected });
    return Object.freeze({
      expected,
      async checkAgain() {
        if (!complete) return unknown();
        try {
          const response = await window.fetch(url.href, { method: "GET", credentials: "same-origin", cache: "no-store", redirect: "error", headers: { Accept: "application/json" } });
          if (!response.ok || response.redirected) return unknown();
          const current = await response.json();
          if (current.eventId !== eventId || !current.values) return unknown();
          for (const field of fields) {
            const key = field[0].toLowerCase() + field.slice(1);
            const value = current.values[key];
            if (typeof value !== "string" && !((field === "Description" || field === "BuyInDescription") && value === null)) return unknown();
          }
          return { state: Object.keys(expected).every(key => expected[key] === current.values[key]) ? "upToDate" : "different", expected };
        } catch { return unknown(); }
      }
    });
  };
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
    if (!(nextRoot instanceof HTMLElement) || !(nextForm instanceof HTMLFormElement)) {
      window.location.assign(responseUrl);
      return { navigated: true, message: "" };
    }

    root.dataset.identitySaveError = nextRoot.dataset.identitySaveError || root.dataset.identitySaveError || "";
    for (const key of ["identityCurrentVersion", "identityScheduleStale", "identityConflicts"])
      root.dataset[key] = nextRoot.dataset[key] || "";
    const importedRoot = document.importNode(nextRoot, true);
    const previousSave = root.querySelector("[data-identity-save]");
    const importedSave = importedRoot.querySelector("[data-identity-save]");
    if (previousSave instanceof HTMLElement && importedSave instanceof HTMLElement)
      importedSave.replaceWith(previousSave);
    root.replaceChildren(...importedRoot.childNodes);
    form = root.querySelector("form");
    review = root.querySelector("[data-identity-timezone-preview]");
    confirmButton = review?.querySelector("[data-identity-timezone-confirm]");
    if (!(form instanceof HTMLFormElement)) {
      window.location.assign(responseUrl);
      return { navigated: true, message: "" };
    }

    guard?.initialize(form);
    previewProposalPending = true;
    bindNavigationLinks();
    hideInlineConfirmation();
    updateOpenConfirmation(review);
    const messages = [...root.querySelectorAll(".validation-summary, .field-error")]
      .map(element => element.textContent.trim()).filter(Boolean);
    return { navigated: false, message: messages.join(" ") };
  };

  let uncertainSubmission = null;
  const readbackMessage = state => state === "upToDate"
    ? root.dataset.identityUpToDate
    : state === "different" ? root.dataset.identityDifferent : root.dataset.identityUnknown;
  const checkAgain = async () => {
    const result = await uncertainSubmission.checkAgain();
    return { succeeded: false, message: readbackMessage(result.state) || root.dataset.identitySaveError || "" };
  };
  const bindUncertainSubmit = () => form.addEventListener("submit", event => {
    if (!uncertainSubmission) return;
    event.preventDefault();
    window.adminConfirmation?.open({
      title: root.dataset.identityCheckAgain,
      description: root.dataset.identityUnknown,
      actionLabel: root.dataset.identityCheckAgain,
      opener: root.querySelector("[data-identity-save]"),
      onConfirm: checkAgain
    });
  });
  bindUncertainSubmit();

  const submitConfirmation = async () => {
    if (uncertainSubmission) return checkAgain();
    if (!(form instanceof HTMLFormElement)) return { succeeded: false, message: root.dataset.identitySaveError || "" };
    const submittedForm = form;
    const data = new FormData(submittedForm);
    data.set("Input.ConfirmTimezoneChange", "true");
    const submitted = window.createIdentityReadbackSession(data, submittedForm.action || window.location.href, root.dataset.identityEventId);
    const markUncertain = () => {
      uncertainSubmission = submitted;
      const action = document.querySelector("[data-admin-confirmation-action]");
      if (action) action.textContent = root.dataset.identityCheckAgain || "";
      return { succeeded: false, message: readbackMessage("unknown") || root.dataset.identitySaveError || "" };
    };
    const finish = guard?.begin(submittedForm);
    try {
      const response = await window.fetch(submittedForm.action || window.location.href, { method: "POST", body: data, credentials: "same-origin" });
      if (!response.ok) return markUncertain();
      if (response.redirected) {
        if (new URL(response.url).pathname !== `/Admin/Events/Manage/${root.dataset.identityEventId}`) return markUncertain();
        window.location.assign(response.url);
        return true;
      }
      const html = await response.text();
      const parsed = new DOMParser().parseFromString(html, "text/html");
      const returned = parsed.querySelector("[data-identity-editor]");
      if (!returned || returned.dataset.identitySaveUncertain === "true") return markUncertain();
      const result = replaceEditorFromResponse(html, response.url);
      bindUncertainSubmit();
      if (result.navigated) return true;
      return { succeeded: false, message: result.message || root.dataset.identitySaveError || "" };
    } catch {
      return markUncertain();
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

/* Shared Admin editor safeguards; route and response handling belong to each editor. */
(() => {
  "use strict";
  // One browser-exit listener, present only while an owning editor has unsaved
  // values. Product confirmations never dispatch or invoke beforeunload.
  const unsavedOwners = new Set();
  let watchingUnload = false;
  const beforeUnload = event => {
    if (!Array.from(unsavedOwners).some(check => check())) return;
    event.preventDefault();
    event.returnValue = "";
  };
  const refreshUnload = () => {
    const dirty = Array.from(unsavedOwners).some(check => check());
    if (dirty === watchingUnload) return;
    watchingUnload = dirty;
    if (dirty) window.addEventListener("beforeunload", beforeUnload);
    else window.removeEventListener("beforeunload", beforeUnload);
  };
  window.watchAdminUnsavedChanges = check => {
    unsavedOwners.add(check);
    refreshUnload();
    return () => { unsavedOwners.delete(check); refreshUnload(); };
  };
  document.addEventListener("input", refreshUnload);
  document.addEventListener("change", refreshUnload);
  document.addEventListener("reset", () => window.setTimeout(refreshUnload, 0));
  if (typeof MutationObserver === "function") {
    new MutationObserver(refreshUnload).observe(document.body, { childList: true, subtree: true });
  }
  window.createAdminEditorGuard = ({ editor, prefix, closeConfirmation, restoreConfirmation, saveError }) => {
    const baselines = new WeakMap();
    const initialized = new WeakSet();
    let pending = false;
    let pendingDirtyForms = [];
    let discardAction = null;
    let discardOpener = null;
    let sharedDiscard = false;
    const find = suffix => editor()?.querySelector(`[data-${prefix}-${suffix}]`);
    const values = form => JSON.stringify(Array.from(new FormData(form))
      .filter(([name]) => name !== "__RequestVerificationToken")
      .map(([name, value]) => {
        if (!(value && typeof value === "object" && "name" in value && "size" in value)) return [name, value];
        const file = { name: String(value.name), size: Number(value.size), type: String(value.type || "") };
        if (file.name || file.size) file.lastModified = Number(value.lastModified || 0);
        return [name, file];
      }));
    const dirtyForms = except => pending
      ? pendingDirtyForms.filter(form => form !== except)
      : Array.from(editor()?.querySelectorAll("form") || []).filter(form => form !== except && baselines.has(form) && baselines.get(form) !== values(form));
    const cancelDiscard = () => {
      if (sharedDiscard) return window.adminConfirmation.cancel();
      const box = find("discard");
      if (!box || box.hidden) return false;
      box.hidden = true;
      discardAction = null;
      restoreConfirmation?.();
      discardOpener?.focus({ preventScroll: true });
      return true;
    };
    const confirmDiscard = action => {
      if (pending) return;
      if (window.adminConfirmation) {
        if (sharedDiscard || window.adminConfirmation.active) return;
        closeConfirmation?.();
        sharedDiscard = true;
        window.adminConfirmation.discard().then(accepted => {
          sharedDiscard = false;
          if (accepted) action?.();
          else restoreConfirmation?.();
          refreshUnload();
        });
        return;
      }
      closeConfirmation?.();
      discardAction = action;
      discardOpener = document.activeElement;
      const box = find("discard");
      if (!box) return;
      box.hidden = false;
      find("keep")?.focus();
    };
    return {
      get pending() { return pending; },
      dirtyForms, cancelDiscard, confirmDiscard,
      initialize(preserveForm = null) {
        const box = find("discard");
        if (box && !initialized.has(box)) {
          initialized.add(box);
          find("keep")?.addEventListener("click", cancelDiscard);
          find("discard-confirm")?.addEventListener("click", () => {
            const action = discardAction;
            cancelDiscard();
            action?.();
          });
          find("discard")?.addEventListener("keydown", event => {
            if (event.key === "Escape") { event.preventDefault(); event.stopPropagation(); cancelDiscard(); }
          });
        }
        editor()?.querySelectorAll("form").forEach(form => {
          if (form !== preserveForm || !baselines.has(form)) baselines.set(form, values(form));
        });
        refreshUnload();
      },
      begin(form) {
        pendingDirtyForms = dirtyForms();
        pending = true;
        refreshUnload();
        const controls = Array.from(editor().querySelectorAll("input, select, textarea, button")).filter(control => !control.disabled);
        controls.forEach(control => { control.disabled = true; });
        form.setAttribute("aria-busy", "true");
        return () => {
          controls.forEach(control => { control.disabled = false; });
          form.removeAttribute("aria-busy");
          pending = false;
          refreshUnload();
        };
      },
      showFailure(message) {
        const feedback = find("feedback");
        if (!feedback) return;
        feedback.textContent = message || saveError() || "";
        feedback.hidden = false;
        feedback.focus();
      }
    };
  };
})();

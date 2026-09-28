/* One Admin confirmation surface. Callers retain server authorization and own mutations. */
(() => {
  "use strict";
  const dialog = document.querySelector("[data-admin-confirmation]");
  if (!(dialog instanceof HTMLDialogElement)) return;
  const form = dialog.querySelector("form");
  const cancel = dialog.querySelector("[data-admin-confirmation-cancel]");
  const action = dialog.querySelector("[data-admin-confirmation-action]");
  const reason = dialog.querySelector("textarea");
  const typed = dialog.querySelector("input");
  const feedback = dialog.querySelector("[data-admin-confirmation-feedback]");
  let current = null;
  let pending = false;

  const finish = accepted => {
    if (!current || pending) return false;
    const previous = current;
    current = null;
    dialog.close();
    document.body.classList.remove("admin-confirmation-open");
    if (previous.editor?.isConnected && !previous.editor.open) previous.editor.showModal();
    if (previous.opener?.isConnected) previous.opener.focus({ preventScroll: true });
    previous.resolve(accepted);
    return true;
  };
  const setPending = value => {
    pending = value;
    form.setAttribute("aria-busy", String(value));
    for (const control of form.elements) control.disabled = value;
  };
  cancel.addEventListener("click", () => finish(false));
  dialog.addEventListener("cancel", event => { event.preventDefault(); finish(false); });
  // The backdrop is not an action: an accidental pointer release must not discard work.
  dialog.addEventListener("keydown", event => {
    if (event.key === "Escape" && !event.defaultPrevented) { event.preventDefault(); event.stopPropagation(); finish(false); }
  });
  // Resume the editor before its existing route guard processes browser Back.
  // Otherwise a suspended dialog would appear closed to that guard.
  window.addEventListener("popstate", () => { if (!pending) finish(false); }, true);
  form.addEventListener("submit", async event => {
    event.preventDefault();
    if (!current || pending || !form.reportValidity()) return;
    if (current.wom && typed.value !== current.wom.value) {
      feedback.textContent = current.wom.label;
      feedback.hidden = false;
      typed.focus();
      return;
    }
    const request = current;
    feedback.hidden = true;
    setPending(true);
    try {
      const result = await request.onConfirm({ reason: reason.value.trim(), confirmation: typed.value });
      setPending(false);
      if (result === true || result?.succeeded === true) finish(true);
      else {
        feedback.textContent = result?.message || dialog.dataset.failure;
        feedback.hidden = false;
        feedback.focus();
      }
    } catch {
      setPending(false);
      feedback.textContent = dialog.dataset.failure;
      feedback.hidden = false;
      feedback.focus();
    }
  });

  window.adminConfirmation = {
    get active() { return current !== null; },
    cancel: () => finish(false),
    // Resolve true only after explicit success. A false result/rejection keeps the
    // confirmation and entered values available for recovery, without another toast.
    open({ title, description, actionLabel, danger = false, requireReason = false,
      wom = null, onConfirm = () => true, opener = document.activeElement } = {}) {
      if (current) return Promise.resolve(false);
      if (!title || !description || !actionLabel) throw new Error("A confirmation requires its consequence and semantic action.");
      if (wom && (!wom.value || !wom.label)) throw new Error("WOM confirmation requires its exact value and localized instruction.");
      const editors = Array.from(document.querySelectorAll("dialog[open]")).filter(item => item !== dialog);
      if (editors.length > 1) throw new Error("Close the existing dialog before confirming.");
      const editor = editors[0] || null;
      dialog.querySelector("#admin-confirmation-title").textContent = title;
      dialog.querySelector("#admin-confirmation-description").textContent = description;
      action.textContent = actionLabel;
      action.classList.toggle("action-danger-outline", danger);
      dialog.querySelector("[data-admin-confirmation-reason-field]").hidden = !requireReason;
      reason.required = requireReason;
      reason.value = "";
      dialog.querySelector("[data-admin-confirmation-typed-field]").hidden = !wom;
      dialog.querySelector("[data-admin-confirmation-typed-label]").textContent = wom?.label || "";
      typed.required = !!wom;
      typed.value = "";
      feedback.hidden = true;
      setPending(false);
      return new Promise(resolve => {
        current = { resolve, editor, opener, onConfirm, wom };
        editor?.close();
        document.body.classList.add("admin-confirmation-open");
        dialog.showModal();
        cancel.focus({ preventScroll: true });
      });
    },
    discard(options = {}) {
      return this.open({ title: dialog.dataset.discardTitle, description: dialog.dataset.discardDescription,
        actionLabel: dialog.dataset.discardAction, danger: true, ...options });
    }
  };
})();

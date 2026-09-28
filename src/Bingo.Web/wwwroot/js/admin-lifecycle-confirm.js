(() => {
  "use strict";

  const initialize = root => {
    const forms = [];
    if (root.matches?.("form[data-lifecycle-confirm]")) forms.push(root);
    root.querySelectorAll?.("form[data-lifecycle-confirm]").forEach(form => forms.push(form));
    forms.forEach(form => {
      if (!(form instanceof HTMLFormElement) || form.dataset.lifecycleConfirmReady === "true") return;
      form.dataset.lifecycleConfirmReady = "true";
      form.addEventListener("submit", event => {
        if (form.dataset.lifecycleConfirmed === "true") return;
        event.preventDefault();
        const confirmFieldName = form.dataset.confirmField;
        const reasonFieldName = form.dataset.confirmReasonField;
        const confirmField = confirmFieldName ? form.querySelector(`[name="${confirmFieldName}"]`) : null;
        const reasonField = reasonFieldName ? form.querySelector(`[name="${reasonFieldName}"]`) : null;
        const wom = form.dataset.confirmWomValue ? {
          value: form.dataset.confirmWomValue,
          label: form.dataset.confirmWomLabel || "Type FETCH exactly to confirm."
        } : null;
        window.adminConfirmation?.open({
          title: form.dataset.confirmTitle,
          description: form.dataset.confirmDescription,
          actionLabel: form.dataset.confirmAction,
          danger: form.dataset.confirmDanger === "true",
          requireReason: form.dataset.confirmRequireReason === "true",
          wom,
          onConfirm: ({ reason, confirmation }) => {
            if (confirmField instanceof HTMLInputElement) confirmField.value = wom ? confirmation : "true";
            if (reasonField instanceof HTMLInputElement || reasonField instanceof HTMLTextAreaElement) reasonField.value = reason || "";
            form.dataset.lifecycleConfirmed = "true";
            HTMLFormElement.prototype.submit.call(form);
            return true;
          }
        });
      });
    });
  };

  initialize(document);
  document.addEventListener("bingo:content-updated", () => initialize(document));
})();

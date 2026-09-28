(() => {
  "use strict";
  const form = document.querySelector("[data-account-transfer]");
  if (!(form instanceof HTMLFormElement)) return;
  const destination = form.querySelector("select");
  const version = form.querySelector("[name='Input.ExpectedAuthorizationVersion']");
  const updateVersion = () => { version.value = destination.selectedOptions[0]?.dataset.authorizationVersion || "0"; };
  destination.addEventListener("change", updateVersion);
  updateVersion();
  let pending = false;
  form.addEventListener("submit", async event => {
    event.preventDefault();
    if (pending || !form.reportValidity()) return;
    pending = true;
    const accepted = await window.adminConfirmation.open({
      title: form.dataset.confirmTitle,
      description: `${destination.selectedOptions[0].textContent}: ${form.dataset.confirmDescription}`,
      actionLabel: form.dataset.confirmAction,
      danger: true,
      opener: event.submitter,
      onConfirm: () => { form.submit(); return true; }
    });
    if (!accepted) pending = false;
  });
})();

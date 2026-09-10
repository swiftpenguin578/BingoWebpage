(() => {
  const sync = (captain) => {
    const scope = captain.closest("form") || document;
    const enabled = captain.type === "checkbox" ? captain.checked : captain.value === "true";
    scope.querySelectorAll("[data-co-captain-field]").forEach((field) => {
      field.hidden = !enabled;
      field.querySelectorAll("[data-co-captain-input]").forEach((input) => { input.disabled = !enabled; });
    });
  };

  document.querySelectorAll("[data-captain-volunteer-input]").forEach((captain) => {
    sync(captain);
  });

  document.addEventListener("change", (event) => {
    const captain = event.target.closest?.("[data-captain-volunteer-input]");
    if (captain) sync(captain);
  });
})();

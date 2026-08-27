(() => {
  const form = document.querySelector(".captain-focus-selector-form");
  const kind = form?.querySelector("[data-captain-focus-target-kind]");
  const target = form?.querySelector("[data-captain-focus-target-value]");
  const label = form?.querySelector("[data-label-tile]");
  const placeholder = target?.querySelector("[data-captain-focus-placeholder]");
  if (!kind || !target || !label || !placeholder) return;

  const options = [...target.querySelectorAll("option[data-captain-focus-target]")];
  const syncTargets = () => {
    const selectedKind = kind.value;
    const labelText = label.dataset[`label${selectedKind}`];
    const placeholderText = target.dataset[`placeholder${selectedKind}`];
    if (!labelText || !placeholderText) return;

    label.textContent = labelText;
    placeholder.textContent = placeholderText;
    target.value = "";
    for (const option of options) {
      const available = option.dataset.captainFocusTarget === selectedKind;
      option.hidden = !available;
      option.disabled = !available;
    }
  };

  kind.addEventListener("change", syncTargets);
  syncTargets();
})();

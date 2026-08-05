(() => {
  "use strict";

  function focusInvalid(form, panel) {
    const invalid = panel?.querySelector(".input-validation-error, [aria-invalid='true']");
    const target = invalid || form.querySelector(".validation-summary");
    if (target?.focus) {
      if (target === form.querySelector(".validation-summary")) target.tabIndex = -1;
      target.focus({ preventScroll: true });
    }
  }

  function validateCurrentStep(form, panel, jquery) {
    const required = [...panel.querySelectorAll("input, select, textarea")]
      .filter((control) => control.hasAttribute("required") || control.hasAttribute("data-val-required"));
    if (!required.length || jquery(required).valid()) return true;
    jquery(form).valid();
    focusInvalid(form, panel);
    return false;
  }

  function navigateForward(currentStep, targetStep, panels, reveal, validate) {
    for (let stepIndex = currentStep; stepIndex < targetStep; stepIndex += 1) {
      reveal(stepIndex);
      if (!validate(panels[stepIndex])) return stepIndex;
    }
    return targetStep;
  }

  function validateAndReveal(form, jquery, reveal) {
    const validator = jquery(form).data("validator");
    if (!validator) return true;

    const originalIgnore = validator.settings.ignore;
    let valid;
    validator.settings.ignore = ":hidden:not([name])";
    try {
      valid = jquery(form).valid();
    } finally {
      validator.settings.ignore = originalIgnore;
    }
    if (valid) return true;

    const invalid = form.querySelector(".input-validation-error, [aria-invalid='true']");
    const invalidPanel = invalid?.closest("[data-create-panel]");
    if (invalidPanel) reveal(Number(invalidPanel.dataset.createPanel));
    const target = invalid || form.querySelector(".validation-summary");
    if (target?.focus) {
      if (target === form.querySelector(".validation-summary")) target.tabIndex = -1;
      target.focus({ preventScroll: true });
    }
    return false;
  }

  const api = { navigateForward, validateAndReveal, validateCurrentStep };
  if (typeof window !== "undefined") window.bingoEventCreateValidation = api;
  if (typeof module !== "undefined" && module.exports) module.exports = api;
})();

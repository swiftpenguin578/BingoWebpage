(() => {
  "use strict";

  document.addEventListener("DOMContentLoaded", () => {
    const root = document.querySelector("[data-event-create]");
    if (!root) return;

    const form = root.querySelector("form");
    const panels = [...root.querySelectorAll("[data-create-panel]")];
    const steps = [...root.querySelectorAll("[data-create-step]")];
    const previous = root.querySelector("[data-create-previous]");
    const next = root.querySelector("[data-create-next]");
    const submit = root.querySelector("[data-create-submit]");
    const progress = root.querySelector("[data-step-progress]");
    let currentStep = findInitialStep();

    root.classList.add("is-guided");
    showStep(currentStep, false);
    initializeSignupCode();
    initializeQuestions();
    initializeSummary();

    steps.forEach((step) => step.addEventListener("click", () => showStep(Number(step.dataset.createStep))));
    previous?.addEventListener("click", () => showStep(currentStep - 1));
    next?.addEventListener("click", () => showStep(currentStep + 1));
    form?.addEventListener("submit", (event) => {
      if (!window.jQuery?.validator?.unobtrusive) return;
      if (window.jQuery(form).valid()) return;
      event.preventDefault();
      const invalidPanel = form.querySelector(".input-validation-error")?.closest("[data-create-panel]");
      if (invalidPanel) showStep(Number(invalidPanel.dataset.createPanel));
    });

    function findInitialStep() {
      const invalid = root.querySelector(".input-validation-error, .field-error:not(:empty)");
      const invalidPanel = invalid?.closest("[data-create-panel]");
      return invalidPanel ? Number(invalidPanel.dataset.createPanel) : 0;
    }

    function showStep(index, focus = true) {
      currentStep = Math.max(0, Math.min(index, panels.length - 1));
      panels.forEach((panel, panelIndex) => {
        const active = panelIndex === currentStep;
        panel.classList.toggle("is-active", active);
        panel.hidden = !active;
      });
      steps.forEach((step, stepIndex) => {
        const active = stepIndex === currentStep;
        step.toggleAttribute("aria-current", active);
        if (active) step.setAttribute("aria-current", "step");
        step.classList.toggle("is-complete", stepIndex < currentStep);
      });
      if (previous) previous.hidden = currentStep === 0;
      if (next) next.hidden = currentStep === panels.length - 1;
      if (submit) submit.hidden = currentStep !== panels.length - 1;
      if (progress) progress.textContent = `${root.dataset.stepWord} ${currentStep + 1} ${root.dataset.ofWord} ${panels.length}`;
      if (currentStep === panels.length - 1) updateSummary();
      if (focus) panels[currentStep]?.querySelector("h2")?.focus?.({ preventScroll: true });
      panels[currentStep]?.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }

    function initializeSignupCode() {
      const toggle = root.querySelector("[data-signup-code-toggle]");
      const field = root.querySelector("[data-signup-code-field]");
      const input = field?.querySelector("input");
      if (!toggle || !field || !input) return;
      const update = () => {
        field.hidden = !toggle.checked;
        input.disabled = !toggle.checked;
        if (!toggle.checked) input.value = "";
      };
      toggle.addEventListener("change", update);
      update();
    }

    function initializeQuestions() {
      const list = root.querySelector("[data-question-list]");
      const empty = root.querySelector("[data-question-empty]");
      const template = document.getElementById("custom-question-template");
      if (!list || !empty || !template) return;

      root.querySelector("[data-add-question]")?.addEventListener("click", () => {
        const fragment = template.content.cloneNode(true);
        list.append(fragment);
        reindexQuestions();
        const card = list.lastElementChild;
        wireQuestion(card);
        card?.querySelector("input")?.focus();
        if (window.jQuery?.validator?.unobtrusive) window.jQuery.validator.unobtrusive.parse(card);
      });
      list.querySelectorAll("[data-custom-question]").forEach(wireQuestion);
      reindexQuestions();

      function wireQuestion(card) {
        card?.querySelector("[data-remove-question]")?.addEventListener("click", () => {
          card.remove();
          reindexQuestions();
        });
        const type = card?.querySelector("[data-question-type]");
        const options = card?.querySelector("[data-choice-options]");
        const updateOptions = () => { if (options) options.hidden = type?.value !== "SingleChoice"; };
        type?.addEventListener("change", updateOptions);
        updateOptions();
      }

      function reindexQuestions() {
        const cards = [...list.querySelectorAll("[data-custom-question]")];
        cards.forEach((card, index) => {
          card.querySelector("[data-question-number]").textContent = `${root.dataset.questionWord} ${index + 1}`;
          card.querySelectorAll("[name], [id], [for], [data-valmsg-for]").forEach((element) => {
            ["name", "id", "for", "data-valmsg-for"].forEach((attribute) => {
              const value = element.getAttribute(attribute);
              if (!value) return;
              element.setAttribute(attribute, value
                .replace(/CustomQuestions\[\d+\]/g, `CustomQuestions[${index}]`)
                .replace(/CustomQuestions_\d+__/g, `CustomQuestions_${index}__`)
                .replace(/__index__/g, String(index)));
            });
          });
        });
        empty.hidden = cards.length > 0;
      }
    }

    function initializeSummary() {
      form?.addEventListener("input", updateSummary);
      form?.addEventListener("change", updateSummary);
      updateSummary();
    }

    function updateSummary() {
      const text = (selector) => root.querySelector(selector)?.value?.trim() || "";
      const set = (key, value) => { const target = root.querySelector(`[data-summary-value="${key}"]`); if (target) target.textContent = value; };
      const dateTime = (selector) => {
        const input = root.querySelector(selector);
        if (input?._flatpickr?.selectedDates?.length) return input._flatpickr.altInput.value;
        return input?.value?.trim() || "—";
      };

      set("name", text("[data-summary-source='name']") || root.dataset.unnamedText);
      set("description", text("[data-summary-source='description']") || root.dataset.noDescriptionText);
      set("signup-window", `${dateTime("#Input_SignupOpensLocal")} – ${dateTime("#Input_SignupClosesLocal")}`);
      const capacity = text("[data-summary-source='capacity']") || "—";
      const waiting = root.querySelector("#Input_WaitingListEnabled")?.checked ? ` · ${root.dataset.waitingListText}` : "";
      set("capacity", `${capacity} ${root.dataset.playersWord}${waiting}`);
      set("event-window", `${dateTime("#Input_EventStartsLocal")} – ${dateTime("#Input_EventEndsLocal")}`);
      const rows = text("[data-summary-source='rows']") || "—";
      const columns = text("[data-summary-source='columns']") || "—";
      set("board", `${rows} × ${columns} ${root.dataset.boardWord}`);
    }
  });
})();

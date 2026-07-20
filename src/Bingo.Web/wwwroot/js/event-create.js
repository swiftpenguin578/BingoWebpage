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
    initializeDatePickers();
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

    function initializeDatePickers() {
      if (typeof window.flatpickr !== "function") return;
      let initialized = false;
      root.querySelectorAll("[data-event-datetime-picker]").forEach((picker) => {
        const dateInput = document.getElementById(picker.dataset.dateTarget);
        const timeInput = document.getElementById(picker.dataset.timeTarget);
        if (!dateInput || !timeInput) return;
        let timeSelect;
        const syncTimeSelect = (dates) => {
          if (!timeSelect || !dates?.length) return;
          const date = dates[0];
          timeSelect.value = `${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
        };
        window.flatpickr(picker, {
          enableTime: true,
          time_24hr: true,
          dateFormat: "Y-m-d H:i",
          altInput: true,
          altFormat: "d/m/Y H:i",
          minuteIncrement: 30,
          disableMobile: true,
          allowInput: false,
          defaultDate: picker.value,
          onReady: (dates, _value, instance) => {
            const timeContainer = instance.timeContainer;
            if (!timeContainer) return;
            instance.calendarContainer.classList.add("event-calendar-picker");
            timeContainer.classList.add("event-calendar-time");
            const label = document.createElement("label");
            label.className = "event-calendar-time-label";
            const labelText = document.createElement("span");
            labelText.textContent = root.dataset.timeWord;
            timeSelect = document.createElement("select");
            timeSelect.className = "event-time-select";
            timeSelect.setAttribute("aria-label", root.dataset.timeWord);
            for (let index = 0; index < 48; index++) {
              const value = `${String(Math.floor(index / 2)).padStart(2, "0")}:${index % 2 === 0 ? "00" : "30"}`;
              timeSelect.add(new Option(value, value));
            }
            timeSelect.addEventListener("change", () => {
              const selectedDate = instance.selectedDates[0] ? new Date(instance.selectedDates[0]) : new Date();
              const [hours, minutes] = timeSelect.value.split(":").map(Number);
              selectedDate.setHours(hours, minutes, 0, 0);
              instance.setDate(selectedDate, true);
            });
            label.append(labelText, timeSelect);
            timeContainer.append(label);
            syncTimeSelect(dates);
          },
          onChange: (dates, value) => {
            const [date, time] = value.split(" ");
            if (date) dateInput.value = date;
            if (time) timeInput.value = time;
            syncTimeSelect(dates);
            updateSummary();
          }
        });
        initialized = true;
      });
      if (initialized) root.classList.add("has-date-pickers");
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
      const dateTime = (pickerSelector, dateSelector, timeSelector) => {
        const picker = root.querySelector(pickerSelector);
        if (root.classList.contains("has-date-pickers") && picker?._flatpickr?.selectedDates?.length)
          return picker._flatpickr.altInput.value;
        const date = text(dateSelector);
        const time = text(timeSelector);
        return [date, time].filter(Boolean).join(" ") || "—";
      };

      set("name", text("[data-summary-source='name']") || root.dataset.unnamedText);
      set("description", text("[data-summary-source='description']") || root.dataset.noDescriptionText);
      set("signup-window", `${dateTime("[data-date-target='Input_SignupOpensDate']", "#Input_SignupOpensDate", "#Input_SignupOpensTime")} – ${dateTime("[data-date-target='Input_SignupClosesDate']", "#Input_SignupClosesDate", "#Input_SignupClosesTime")}`);
      const capacity = text("[data-summary-source='capacity']") || "—";
      const waiting = root.querySelector("#Input_WaitingListEnabled")?.checked ? ` · ${root.dataset.waitingListText}` : "";
      set("capacity", `${capacity} ${root.dataset.playersWord}${waiting}`);
      set("event-window", `${dateTime("[data-date-target='Input_EventStartsDate']", "#Input_EventStartsDate", "#Input_EventStartsTime")} – ${dateTime("[data-date-target='Input_EventEndsDate']", "#Input_EventEndsDate", "#Input_EventEndsTime")}`);
      const teams = text("[data-summary-source='teams']");
      const teamSize = text("[data-summary-source='team-size']");
      set("teams", teams && teamSize
        ? `${teams} ${root.dataset.teamsWord} · ${teamSize} ${root.dataset.perTeamText}`
        : root.dataset.notSetText);
      const rows = text("[data-summary-source='rows']") || "—";
      const columns = text("[data-summary-source='columns']") || "—";
      set("board", `${rows} × ${columns} ${root.dataset.boardWord}`);
    }
  });
})();

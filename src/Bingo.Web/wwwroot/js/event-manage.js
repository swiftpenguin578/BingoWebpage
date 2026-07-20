(() => {
  "use strict";

  initialize(document);
  document.addEventListener("bingo:content-updated", () => initialize(document));
  document.addEventListener("change", (event) => {
    const control = event.target.closest("[data-auto-submit]");
    if (!(control instanceof HTMLSelectElement)) return;
    control.form?.requestSubmit();
  });

  function initialize(root) {
    initializeDatePickers(root);
    initializeDraftSizeConfirmation(root);
    synchronizeRosterRoleDisplays(root);

    root.querySelectorAll("[data-participant-group]").forEach((group) => {
      if (group.dataset.interactionsReady === "true") return;
      group.dataset.interactionsReady = "true";
      const search = group.querySelector("[data-participant-search]");
      const tbody = group.querySelector("tbody");
      const sortButtons = [...group.querySelectorAll("[data-participant-sort]")];
      if (!tbody) return;

      const noMatches = document.createElement("tr");
      noMatches.className = "participant-search-empty";
      noMatches.hidden = true;
      noMatches.innerHTML = `<td colspan="7">${group.dataset.noMatches}</td>`;
      tbody.append(noMatches);

      search?.addEventListener("input", filterRows);

      sortButtons.forEach((button) => button.addEventListener("click", () => {
        const nextDirection = button.dataset.direction === "asc" ? "desc" : "asc";
        sortButtons.forEach((item) => {
          delete item.dataset.direction;
          item.closest("th")?.removeAttribute("aria-sort");
        });
        button.dataset.direction = nextDirection;
        button.closest("th")?.setAttribute("aria-sort", nextDirection === "asc" ? "ascending" : "descending");

        const index = Number(button.dataset.sortIndex);
        const type = button.dataset.sortType;
        const multiplier = nextDirection === "asc" ? 1 : -1;
        const rows = participantRows();
        rows.sort((left, right) => compareValues(valueAt(left, index), valueAt(right, index), type) * multiplier);
        rows.forEach((row) => tbody.insertBefore(row, noMatches));
        filterRows();
      }));

      function filterRows() {
        const query = search?.value.trim().toLocaleLowerCase() ?? "";
        const rows = participantRows();
        let visible = 0;
        rows.forEach((row) => {
          const matches = !query || row.textContent.toLocaleLowerCase().includes(query);
          row.hidden = !matches;
          if (matches) visible++;
        });
        noMatches.hidden = visible > 0 || rows.length === 0;
      }

      function participantRows() {
        return [...group.querySelectorAll("[data-participant-row]")];
      }

      function valueAt(row, index) {
        const cell = row.cells[index];
        return cell?.dataset.sortValue ?? cell?.textContent.trim() ?? "";
      }

      function compareValues(left, right, type) {
        if (type === "number" || type === "date") return Number(left) - Number(right);
        return left.localeCompare(right, undefined, { numeric: true, sensitivity: "base" });
      }
    });
  }

  function synchronizeRosterRoleDisplays(root) {
    root.querySelectorAll("form[data-role-display]").forEach((form) => {
      const select = form.querySelector("select[name='role']");
      const display = document.querySelector(form.dataset.roleDisplay);
      if (!(select instanceof HTMLSelectElement) || !display) return;
      display.textContent = select.selectedOptions[0]?.textContent?.trim() || select.value;
    });
  }

  function initializeDraftSizeConfirmation(root) {
    root.querySelectorAll("[data-draft-size-form]").forEach((form) => {
      if (form.dataset.confirmationReady === "true") return;
      form.dataset.confirmationReady = "true";
      form.addEventListener("submit", (event) => {
        const currentCount = Number(form.dataset.currentTeamCount);
        const requestedCount = Number(form.elements.namedItem("teamCount")?.value);
        if (!Number.isFinite(requestedCount) || requestedCount >= currentCount) return;

        const removeCount = currentCount - requestedCount;
        const emptyTeams = [...document.querySelectorAll(".team-card[data-draft-team='true'][data-member-count='0']")]
          .map((team) => team.dataset.teamName)
          .filter(Boolean)
          .sort((left, right) => right.localeCompare(left, undefined, { numeric: true, sensitivity: "base" }))
          .slice(0, removeCount);

        // The server will show the normal validation message when there are
        // not enough empty teams to complete the requested reduction.
        if (emptyTeams.length !== removeCount) return;
        const message = `Reducing the draft to ${requestedCount} teams will remove: ${emptyTeams.join(", ")}. Continue?`;
        if (!window.confirm(message)) event.preventDefault();
      });
    });
  }

  function initializeDatePickers(root) {
    if (typeof window.flatpickr !== "function") return;
    root.querySelectorAll("[data-event-manage-datetime-picker]").forEach((input) => {
      if (input._flatpickr) return;
      let timeSelect;
      const syncTimeSelect = (dates) => {
        if (!timeSelect || !dates?.length) return;
        const date = dates[0];
        timeSelect.value = `${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
      };
      window.flatpickr(input, {
        enableTime: true,
        time_24hr: true,
        dateFormat: "Z",
        altInput: true,
        altFormat: "d/m/Y H:i",
        minuteIncrement: 30,
        disableMobile: true,
        allowInput: false,
        defaultDate: input.value,
        onReady: (dates, _value, instance) => {
          const timeContainer = instance.timeContainer;
          if (!timeContainer) return;
          instance.calendarContainer.classList.add("event-calendar-picker");
          timeContainer.classList.add("event-calendar-time");
          const label = document.createElement("label");
          label.className = "event-calendar-time-label";
          const labelText = document.createElement("span");
          labelText.textContent = input.dataset.timeLabel || "Time";
          timeSelect = document.createElement("select");
          timeSelect.className = "event-time-select";
          timeSelect.setAttribute("aria-label", labelText.textContent);
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
        onChange: syncTimeSelect
      });
    });
  }
})();

(() => {
  "use strict";

  initialize(document);
  document.addEventListener("bingo:content-updated", () => initialize(document));
  document.addEventListener("change", (event) => {
    const control = event.target.closest("[data-auto-submit]");
    if (!(control instanceof HTMLSelectElement) && !(control instanceof HTMLInputElement && control.type === "checkbox")) return;
    control.form?.requestSubmit();
  });

  function initialize(root) {
    initializeDatePickers(root);
    initializeDraftSizeConfirmation(root);
    synchronizeRosterRoleDisplays(root);

    root.querySelectorAll("form.participant-payment-form").forEach((form) => {
      const state = form.querySelector("[data-save-state]");
      form.addEventListener("submit", () => { if (state) state.textContent = "Saving"; }, { once: true });
    });

    root.querySelectorAll("form[data-participant-filter-form]").forEach((form) => {
      if (form.dataset.filterReady === "true") return;
      form.dataset.filterReady = "true";
      form.querySelectorAll("select").forEach((control) => control.addEventListener("change", () => form.requestSubmit()));
      const search = form.querySelector("input[type='search']");
      let timer;
      search?.addEventListener("input", () => { window.clearTimeout(timer); timer = window.setTimeout(() => form.requestSubmit(), 350); });
      form.addEventListener("submit", () => { if (!form.action.includes("#players")) form.action = `${form.action.split("#")[0]}#players`; });
    });

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
      window.initializeBingoDateTimePicker(input, {
        dateFormat: "Z",
        altInput: true,
        altFormat: "d/m/Y H:i",
        defaultDate: input.value,
        timeLabel: input.dataset.timeLabel || "Time"
      });
    });
  }
})();

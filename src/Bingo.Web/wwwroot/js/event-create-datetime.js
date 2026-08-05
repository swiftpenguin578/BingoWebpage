(() => {
  "use strict";

  const defaultTime = "12:30";

  function format(value) {
    const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(value || "");
    return match ? `${match[3]}/${match[2]}/${match[1]} ${match[4]}:${match[5]}` : "—";
  }

  function sync(control) {
    const canonical = control.querySelector("[data-datetime-canonical]");
    const date = control.querySelector("[data-datetime-date]");
    const time = control.querySelector("[data-datetime-time]");
    if (!canonical || !date || !time) return;
    canonical.value = date.value && time.value ? `${date.value}T${time.value}` : "";
    canonical.dispatchEvent(new Event("input", { bubbles: true }));
    canonical.dispatchEvent(new Event("change", { bubbles: true }));
  }

  function initializeControl(control) {
    const canonical = control.querySelector("[data-datetime-canonical]");
    const date = control.querySelector("[data-datetime-date]");
    const time = control.querySelector("[data-datetime-time]");
    if (!canonical || !date || !time) return;

    canonical.tabIndex = -1;
    canonical.setAttribute("aria-hidden", "true");
    const [dateValue, timeValue] = (canonical.value || "").split("T");
    date.value = dateValue || "";
    time.value = timeValue ? timeValue.slice(0, 5) : defaultTime;
    time.defaultValue = time.value;

    date.addEventListener("change", () => sync(control));
    time.addEventListener("input", () => sync(control));
    time.addEventListener("change", () => sync(control));
    if (date.value && !timeValue) sync(control);
  }

  function initializeControls(root, datePicker) {
    const controls = [...(root?.querySelectorAll("[data-datetime-control]") || [])];
    if (!controls.length) return;
    root.classList.add("has-event-datetime-adapters");
    controls.forEach((control) => {
      initializeControl(control);
      const date = control.querySelector("[data-datetime-date]");
      if (typeof datePicker === "function" && !date._flatpickr) {
        datePicker(date, {
          dateFormat: "Y-m-d",
          altInput: true,
          altFormat: "d/m/Y",
          disableMobile: true,
          allowInput: false,
          onChange: () => sync(control)
        });
      }
    });
  }

  const api = { defaultTime, format, initializeControl, initializeControls, sync };
  if (typeof window !== "undefined") window.bingoEventDateTime = api;
  if (typeof module !== "undefined" && module.exports) module.exports = api;
})();

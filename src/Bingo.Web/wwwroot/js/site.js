// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
document.addEventListener("DOMContentLoaded", () => {
  if (typeof window.flatpickr === "function") {
    document.querySelectorAll("[data-date-time-picker]").forEach((input) => {
      window.flatpickr(input, {
        enableTime: true,
        enableSeconds: true,
        time_24hr: true,
        dateFormat: "Y-m-d H:i:S",
        altInput: true,
        altFormat: "d/m/Y H:i:S",
        defaultDate: input.value || null,
        minDate: input.dataset.minDate || null,
        maxDate: input.dataset.maxDate || null,
        minuteIncrement: 1,
        disableMobile: true,
        allowInput: false
      });
    });
  }

  const menus = [...document.querySelectorAll(".nav-popover details")];
  for (const menu of menus) {
    menu.addEventListener("toggle", () => {
      if (!menu.open) return;
      for (const other of menus) if (other !== menu) other.open = false;
    });
  }
  document.addEventListener("click", event => {
    for (const menu of menus) if (menu.open && !menu.contains(event.target)) menu.open = false;
  });
});

function initializeAdminReviewQueue(page) {
  if (!page || page.dataset.reviewQueueReady === "true") return;
  page.dataset.reviewQueueReady = "true";

  const form = page.querySelector("[data-admin-review-filter-form]");
  const search = form?.querySelector("[data-admin-review-search]");
  const status = form?.querySelector("[data-admin-review-status]");
  const clear = form?.querySelector("[data-admin-search-clear]");
  const rows = [...page.querySelectorAll("[data-admin-review-row]")];
  const detailLinks = [...page.querySelectorAll("a.admin-review-submission-link, a.event-overview-row-action")];
  const table = page.querySelector("[data-admin-review-table-wrap]");
  const empty = page.querySelector("[data-admin-review-empty]");
  if (!form || !search || !status) return;

  const normalize = value => String(value ?? "").trim().toLocaleLowerCase();

  const updateDetailLinks = eventId => {
    detailLinks.forEach(link => {
      const href = link.getAttribute?.("href") ?? link.href;
      if (!href) return;
      const next = new URL(href, window.location.href);
      if (eventId) next.searchParams.set("eventId", eventId);
      else next.searchParams.delete("eventId");
      if (search.value.trim()) next.searchParams.set("search", search.value.trim());
      else next.searchParams.delete("search");
      if (status.value.trim()) next.searchParams.set("status", status.value.trim());
      else next.searchParams.delete("status");
      link.href = `${next.pathname}${next.search}${next.hash}`;
    });
  };

  const updateUrl = () => {
    const next = new URL(window.location.href);
    const eventId = page.dataset.eventId || next.searchParams.get("eventId") || "";
    if (eventId) next.searchParams.set("eventId", eventId);
    else next.searchParams.delete("eventId");
    if (search.value.trim()) next.searchParams.set("search", search.value.trim());
    else next.searchParams.delete("search");
    if (status.value.trim()) next.searchParams.set("status", status.value.trim());
    else next.searchParams.delete("status");
    window.history.replaceState(window.history.state, "", `${next.pathname}${next.search}${next.hash}`);
    updateDetailLinks(eventId);
    if (clear) clear.hidden = !search.value.trim();
  };

  const applyFilters = () => {
    const query = normalize(search.value);
    const selectedStatus = normalize(status.value);
    let visible = 0;
    rows.forEach(row => {
      const matchesSearch = !query || [row.dataset.reviewTeam, row.dataset.reviewPlayer, row.dataset.reviewTile]
        .some(value => normalize(value).includes(query));
      const matchesStatus = !selectedStatus || normalize(row.dataset.reviewStatus) === selectedStatus;
      row.hidden = !(matchesSearch && matchesStatus);
      if (!row.hidden) visible++;
    });
    if (table) table.hidden = visible === 0;
    if (empty) empty.hidden = visible > 0;
    updateUrl();
  };

  search.addEventListener("input", applyFilters);
  status.addEventListener("change", applyFilters);
  clear?.addEventListener("click", event => {
    event.preventDefault();
    search.value = "";
    applyFilters();
    search.focus({ preventScroll: true });
  });
  form.addEventListener("submit", event => event.preventDefault());
  applyFilters();
}

if (typeof module !== "undefined" && module.exports) {
  module.exports = { initializeAdminReviewQueue };
} else if (typeof document !== "undefined") {
  const initialize = () => document.querySelectorAll("[data-admin-review-queue]").forEach(initializeAdminReviewQueue);
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initialize);
  else initialize();
}

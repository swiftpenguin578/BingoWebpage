(function () {
  function initialize(root = document, windowObject = window, documentObject = document) {
    const region = root.querySelector("[data-public-recent-drops]");
    if (!region) return;

    const toolbar = root.querySelector("[data-public-recent-drops-toolbar]");
    const form = toolbar?.matches?.("[data-public-recent-drops-filter-form]")
      ? toolbar
      : toolbar?.querySelector("[data-public-recent-drops-filter-form]");
    const search = toolbar?.querySelector("[data-public-recent-drops-search]");
    const team = toolbar?.querySelector("[data-public-recent-drops-team]");
    const clear = toolbar?.querySelector("[data-public-recent-drops-clear]");
    const participantLabel = region.dataset.participantLabel || "Participant";
    const evidenceAltTemplate = region.dataset.evidenceAltTemplate || "Approved evidence from {0}";
    const justNowLabel = region.dataset.justNowLabel || "Just now";
    const eventId = root.querySelector("[data-progress-event]")?.dataset.progressEvent || documentObject.querySelector?.("[data-progress-event]")?.dataset.progressEvent;
    const unit = (value, singular) => `${value} ${value === 1 ? singular : `${singular}s`}`;
    const joinUnits = (first, second) => second ? `${first} ${second}` : first;
    const formatElapsed = submittedAt => {
      const submittedTime = Date.parse(submittedAt);
      if (!Number.isFinite(submittedTime)) return justNowLabel;
      const elapsed = Math.max(0, Date.now() - submittedTime);
      const totalMinutes = Math.floor(elapsed / 60000);
      if (totalMinutes === 0) return justNowLabel;
      if (totalMinutes < 60) return `${totalMinutes} min ago`;

      const totalHours = Math.floor(totalMinutes / 60);
      if (totalHours < 24) return `${unit(totalHours, "hr")} ago`;

      const totalDays = Math.floor(totalHours / 24);
      if (totalDays < 7) return `${joinUnits(unit(totalDays, "day"), totalHours % 24 === 0 ? null : unit(totalHours % 24, "hr"))} ago`;

      const totalWeeks = Math.floor(totalDays / 7);
      if (totalDays < 30) return `${joinUnits(unit(totalWeeks, "week"), totalDays % 7 === 0 ? null : unit(totalDays % 7, "day"))} ago`;

      const totalMonths = Math.floor(totalDays / 30);
      if (totalDays < 365) return `${unit(totalMonths, "month")} ago`;

      const years = Math.floor(totalDays / 365);
      const months = Math.floor(totalDays % 365 / 30);
      return `${joinUnits(unit(years, "year"), months === 0 ? null : unit(months, "month"))} ago`;
    };
    let pending = false;
    let livePending = false;
    let liveQueued = false;
    let debounceTimer;
    let queuedFilterTarget;
    let newRowsRevision = 0;
    const newRowRevisions = new Map();

    const cancelDebounce = () => {
      clearTimeout(debounceTimer);
      debounceTimer = undefined;
    };

    const syncClear = () => {
      if (search && clear) clear.hidden = search.value.length === 0;
    };

    const filterUrl = () => {
      const target = new URL(windowObject.location.href);
      target.searchParams.set("view", "drops");
      target.searchParams.set("dropCount", "25");
      const searchValue = search?.value.trim() || "";
      const teamValue = team?.value || "";
      if (searchValue) target.searchParams.set("dropSearch", searchValue);
      else target.searchParams.delete("dropSearch");
      if (teamValue) target.searchParams.set("dropTeam", teamValue);
      else target.searchParams.delete("dropTeam");
      return target;
    };

    const replaceFeed = async (target, trigger = null) => {
      const currentRegion = root.querySelector("[data-public-recent-drops]");
      if (!currentRegion) return;
      if (pending) {
        if (!trigger) queuedFilterTarget = target;
        return;
      }

      const scrollX = windowObject.scrollX;
      const scrollY = windowObject.scrollY;
      pending = true;
      currentRegion.setAttribute("aria-busy", "true");
      trigger?.setAttribute("aria-disabled", "true");

      try {
        const response = await windowObject.fetch(target.href, {
          credentials: "same-origin",
          headers: { Accept: "text/html", "X-Requested-With": "XMLHttpRequest" }
        });
        if (!response.ok) throw new Error("Recent drops request failed.");

        const parsed = new DOMParser().parseFromString(await response.text(), "text/html");
        const nextRegion = parsed.querySelector("[data-public-recent-drops]");
        if (!nextRegion) throw new Error("Recent drops region was not returned.");

        const replacement = documentObject.importNode(nextRegion, true);
        currentRegion.replaceWith(replacement);
        const nextDivider = parsed.querySelector("[data-public-recent-drops-first-divider]");
        const currentDivider = root.querySelector("[data-public-recent-drops-first-divider]");
        if (nextDivider && currentDivider) currentDivider.replaceWith(documentObject.importNode(nextDivider, true));
        const nextResult = parsed.querySelector("[data-public-recent-drops-result]");
        const result = root.querySelector("[data-public-recent-drops-result]");
        if (nextResult && result) result.textContent = nextResult.textContent;
        windowObject.history?.replaceState?.({}, "", target.href);

        const focusKey = trigger?.dataset.publicRecentDropsFocusTarget;
        const focusTarget = focusKey
          ? replacement.querySelector(`[data-public-recent-drops-focus="${focusKey}"]`)
          : null;
        const fallbackFocusTarget = replacement.querySelector("[data-public-recent-drops-focus=\"back\"]") || replacement;
        if (focusTarget || trigger) (focusTarget || fallbackFocusTarget).focus?.({ preventScroll: true });
        const restoreScroll = () => windowObject.scrollTo?.(scrollX, scrollY);
        restoreScroll();
        windowObject.requestAnimationFrame?.(restoreScroll);
      } catch {
        if (!queuedFilterTarget) windowObject.location.assign(target.href);
      } finally {
        currentRegion.removeAttribute("aria-busy");
        root.querySelector("[data-public-recent-drops]")?.removeAttribute("aria-busy");
        trigger?.removeAttribute("aria-disabled");
        pending = false;
        const queuedTarget = queuedFilterTarget;
        queuedFilterTarget = undefined;
        if (queuedTarget) replaceFeed(queuedTarget);
        else if (eventId) reconcileLive();
      }
    };

    const applyFilters = () => replaceFeed(filterUrl());
    const reconcileNewRows = async ids => {
      if (!eventId || ids.length === 0) return;
      const revision = ++newRowsRevision;
      ids.forEach(id => newRowRevisions.set(id, revision));
      for (let index = 0; index < ids.length; index += 100) {
        const batch = ids.slice(index, index + 100);
        try {
          const response = await windowObject.fetch(`/api/drop-announcements/new?eventId=${encodeURIComponent(eventId)}&submissionIds=${batch.join(",")}`, { credentials: "same-origin", headers: { Accept: "application/json" } });
          if (!response.ok) continue;
          const newIds = new Set(await response.json());
          batch.forEach(id => {
            if (newRowRevisions.get(id) !== revision) return;
            const card = root.querySelector(`[data-public-recent-drop-id="${id}"]`);
            const title = card?.querySelector(".public-ui-recent-drop-title");
            const badge = title?.querySelector(".public-drop-new");
            if (newIds.has(id)) {
              if (title && !badge) { const nextBadge = documentObject.createElement("span"); nextBadge.className = "public-drop-new"; nextBadge.textContent = region.dataset.newLabel || "NEW"; title.append(" ", nextBadge); }
            } else badge?.remove();
          });
        } catch { return; }
      }
    };
    const groupKeyFor = submittedAt => {
      const submittedTime = Date.parse(submittedAt || "");
      if (!Number.isFinite(submittedTime)) return "older";
      const elapsed = Date.now() - submittedTime;
      if (elapsed <= 60 * 60 * 1000) return "last-hour";
      if (elapsed <= 24 * 60 * 60 * 1000) return "last-24-hours";
      return "older";
    };
    const requestedWindowSize = () => {
      const parsed = Number.parseInt(new URL(windowObject.location.href).searchParams.get("dropCount") || "", 10);
      return Number.isSafeInteger(parsed) && parsed > 0 ? Math.max(25, parsed) : 25;
    };
    const renderLiveFeed = (currentRegion, cards) => {
      const groups = [
        { key: "last-hour", label: currentRegion.dataset.lastHourLabel || "Last hour", cards: [] },
        { key: "last-24-hours", label: currentRegion.dataset.last24HoursLabel || "Last 24 hours", cards: [] },
        { key: "older", label: currentRegion.dataset.olderDropsLabel || "Older drops", cards: [] }
      ];
      cards.forEach(card => groups.find(group => group.key === groupKeyFor(card.dataset.publicRecentDropSubmittedAt))?.cards.push(card));
      const visibleGroups = groups.filter(group => group.cards.length > 0);
      let feed = currentRegion.querySelector(".public-ui-recent-drop-feed");
      const firstDivider = root.querySelector("[data-public-recent-drops-first-divider]");
      if (visibleGroups.length === 0) {
        feed?.remove();
        if (firstDivider) {
          firstDivider.id = "live-recent-drops-empty-heading";
          firstDivider.dataset.publicRecentDropsGroupHeading = "";
          const label = firstDivider.querySelector("span");
          if (label) label.textContent = currentRegion.dataset.dropsLabel || "Drops";
        }
        return;
      }
      currentRegion.querySelector(".public-feature-empty")?.remove();
      if (!feed) {
        feed = documentObject.createElement("div");
        feed.className = "public-ui-recent-drop-feed";
        currentRegion.prepend(feed);
      }
      if (!firstDivider) return;

      const existingSections = new Map([...feed.querySelectorAll("[data-public-recent-drop-group]")]
        .map(section => [section.dataset.publicRecentDropGroup, section]));
      const orderedSections = [];
      visibleGroups.forEach((group, index) => {
        group.cards.sort((left, right) => {
          const leftTime = Date.parse(left.dataset.publicRecentDropSubmittedAt || "");
          const rightTime = Date.parse(right.dataset.publicRecentDropSubmittedAt || "");
          return rightTime - leftTime || (right.dataset.publicRecentDropId || "").localeCompare(left.dataset.publicRecentDropId || "");
        });
        const section = existingSections.get(group.key) || documentObject.createElement("section");
        section.className = "public-ui-recent-drop-group";
        section.dataset.publicRecentDropGroup = group.key;
        const grid = section.querySelector(".public-ui-recent-drop-grid") || documentObject.createElement("div");
        grid.className = "public-ui-recent-drop-grid";
        grid.replaceChildren(...group.cards);
        if (index === 0) {
          const oldHeading = section.querySelector("[data-public-recent-drops-group-heading]");
          oldHeading?.remove();
          firstDivider.id = `live-recent-drops-${group.key}-heading`;
          firstDivider.dataset.publicRecentDropsGroupHeading = group.key;
          const label = firstDivider.querySelector("span");
          if (label) label.textContent = group.label;
          section.setAttribute("aria-labelledby", firstDivider.id);
          section.replaceChildren(grid);
        } else {
          let heading = section.querySelector("[data-public-recent-drops-group-heading]");
          if (!heading) {
            heading = documentObject.createElement("h3");
            heading.className = "public-ui-recent-drop-divider";
            heading.dataset.publicRecentDropsGroupHeading = group.key;
            const label = documentObject.createElement("span");
            heading.append(label);
          }
          heading.id = `live-recent-drops-${group.key}-heading`;
          heading.dataset.publicRecentDropsGroupHeading = group.key;
          const label = heading.querySelector("span");
          if (label) label.textContent = group.label;
          section.setAttribute("aria-labelledby", heading.id);
          section.replaceChildren(heading, grid);
        }
        orderedSections.push(section);
      });
      feed.replaceChildren(...orderedSections);
    };
    const reconcileLive = async () => {
      if (!eventId || new URL(windowObject.location.href).searchParams.get("view") !== "drops") return;
      if (pending) { liveQueued = true; return; }
      if (livePending) { liveQueued = true; return; }
      liveQueued = false;
      const currentRegion = root.querySelector("[data-public-recent-drops]");
      if (!currentRegion) return;
      const existingCards = [...currentRegion.querySelectorAll("[data-public-recent-drop-id]")];
      const existingIds = new Set(existingCards.map(card => card.dataset.publicRecentDropId));
      const target = new URL(`/api/public/events/${encodeURIComponent(windowObject.location.pathname.split("/")[2] || "")}/recent-drops`, windowObject.location.href);
      if (search?.value.trim()) target.searchParams.set("dropSearch", search.value.trim());
      if (team?.value) target.searchParams.set("dropTeam", team.value);
      livePending = true;
      try {
        const windowSize = requestedWindowSize();
        const loadedIds = [...existingIds];
        const loadedBatches = [];
        for (let index = 0; index < loadedIds.length; index += 100) loadedBatches.push(loadedIds.slice(index, index + 100));
        if (!loadedBatches.length) loadedBatches.push([]);
        const invalidIds = new Set();
        let payload = { drops: [] };
        for (let offset = 0; offset < windowSize; offset += 100) {
          target.searchParams.set("offset", String(offset));
          target.searchParams.set("limit", String(Math.min(100, windowSize - offset)));
          target.searchParams.set("loadedSubmissionIds", offset === 0 ? loadedBatches[0].join(",") : "");
          const response = await windowObject.fetch(target, { credentials: "same-origin", headers: { Accept: "application/json" } });
          if (!response.ok) return;
          const result = await response.json();
          if (offset === 0) {
            payload = result;
            if (Array.isArray(result.validSubmissionIds)) {
              const validIds = new Set(result.validSubmissionIds);
              loadedBatches[0].forEach(id => { if (!validIds.has(id)) invalidIds.add(id); });
            }
          } else if (Array.isArray(result.drops)) {
            payload.drops.push(...result.drops);
          }
        }
        for (const batch of loadedBatches.slice(1)) {
          target.searchParams.set("offset", "0");
          target.searchParams.set("limit", "1");
          target.searchParams.set("loadedSubmissionIds", batch.join(","));
          const response = await windowObject.fetch(target, { credentials: "same-origin", headers: { Accept: "application/json" } });
          if (!response.ok) return;
          const result = await response.json();
          if (Array.isArray(result.validSubmissionIds)) {
            const validIds = new Set(result.validSubmissionIds);
            batch.forEach(id => { if (!validIds.has(id)) invalidIds.add(id); });
          }
        }
        // A newer invalidation or filter/navigation supersedes this read as a whole.
        if (liveQueued || pending || currentRegion !== root.querySelector("[data-public-recent-drops]") ||
          (search?.value.trim() || "") !== (target.searchParams.get("dropSearch") || "") ||
          (team?.value || "") !== (target.searchParams.get("dropTeam") || "")) return;
        const scrollX = windowObject.scrollX;
        const scrollY = windowObject.scrollY;
        existingCards.forEach(card => { if (invalidIds.has(card.dataset.publicRecentDropId)) card.remove(); });
        const liveSince = Date.parse(currentRegion.dataset.publicRecentDropsSince || "");
        const incoming = (payload.drops || []).filter(drop => {
          if (existingIds.has(drop.submissionId) || invalidIds.has(drop.submissionId)) return false;
          const reviewedAt = Date.parse(drop.reviewedAt || "");
          return Number.isFinite(liveSince) && Number.isFinite(reviewedAt) && reviewedAt >= liveSince;
        });
        const incomingCards = incoming.map(drop => {
          const article = documentObject.createElement("article");
          article.className = "public-ui-surface public-ui-surface--charcoal public-ui-recent-drop-card";
          article.dataset.publicRecentDropId = drop.submissionId;
          article.dataset.publicRecentDropSubmittedAt = drop.submittedAt;
          article.dataset.publicRecentDropReviewedAt = drop.reviewedAt;
          const title = drop.dropName || drop.tileName;
          const heading = drop.bossName ? `${title} · ${drop.bossName}` : title;
          const thumbnail = documentObject.createElement(drop.evidenceAssetId ? "a" : "div");
          thumbnail.className = "public-ui-recent-drop-thumbnail";
          if (drop.evidenceAssetId) {
            thumbnail.href = `/Evidence/${drop.evidenceAssetId}`;
            thumbnail.dataset.evidenceImage = thumbnail.href;
            thumbnail.dataset.evidenceAlt = evidenceAltTemplate.replace("{0}", drop.playerName || participantLabel);
            thumbnail.dataset.evidenceDrop = heading;
            thumbnail.dataset.evidencePlayer = drop.playerName || participantLabel;
            thumbnail.dataset.evidenceTeam = drop.teamName;
            thumbnail.dataset.evidenceSubmissionId = drop.submissionId;
            thumbnail.dataset.evidenceEventId = eventId;
            const image = documentObject.createElement("img"); image.src = thumbnail.href; image.alt = thumbnail.dataset.evidenceAlt; thumbnail.append(image);
          }
          const content = documentObject.createElement("div"); content.className = "public-ui-recent-drop-row-content";
          const headingNode = documentObject.createElement("strong"); headingNode.className = "public-ui-component-title public-ui-recent-drop-title"; headingNode.textContent = title;
          const context = documentObject.createElement("span"); context.className = "public-ui-supporting-text public-ui-recent-drop-activity"; context.textContent = drop.bossName || "";
          const metadata = documentObject.createElement("div"); metadata.className = "public-ui-recent-drop-metadata";
          const player = documentObject.createElement("span"); player.className = "public-ui-supporting-text public-ui-recent-drop-player"; player.textContent = `${drop.playerName || participantLabel} · ${formatElapsed(drop.submittedAt)}`;
          metadata.append(player); content.append(headingNode, context, metadata); article.append(thumbnail, content); return article;
        });
        const visibleCards = existingCards.filter(card => !invalidIds.has(card.dataset.publicRecentDropId)).concat(incomingCards)
          .sort((left, right) => {
            const leftTime = Date.parse(left.dataset.publicRecentDropSubmittedAt || "");
            const rightTime = Date.parse(right.dataset.publicRecentDropSubmittedAt || "");
            return rightTime - leftTime || (right.dataset.publicRecentDropId || "").localeCompare(left.dataset.publicRecentDropId || "");
          })
          .slice(0, windowSize);
        renderLiveFeed(currentRegion, visibleCards);
        windowObject.scrollTo?.(scrollX, scrollY); windowObject.requestAnimationFrame?.(() => windowObject.scrollTo?.(scrollX, scrollY));
        await reconcileNewRows([...currentRegion.querySelectorAll("[data-public-recent-drop-id]")].map(card => card.dataset.publicRecentDropId));
      } catch { }
      finally {
        livePending = false;
        if (liveQueued && !pending) { liveQueued = false; reconcileLive(); }
      }
    };
    if (form) form.addEventListener("submit", event => { event.preventDefault(); cancelDebounce(); return applyFilters(); });
    if (search) {
      search.addEventListener("input", () => {
        syncClear();
        cancelDebounce();
        debounceTimer = setTimeout(() => { debounceTimer = undefined; applyFilters(); }, 250);
      });
    }
    if (team) team.addEventListener("change", () => { cancelDebounce(); return applyFilters(); });
    if (clear) clear.addEventListener("click", () => {
      if (!search) return;
      cancelDebounce();
      search.value = "";
      syncClear();
      search.focus({ preventScroll: true });
      return applyFilters();
    });
    syncClear();
    const initialCards = root.querySelectorAll?.("[data-public-recent-drop-id]") || [];
    reconcileNewRows([...initialCards].map(card => card.dataset.publicRecentDropId));
    windowObject.addEventListener?.("bingo-progress-changed", reconcileLive);
    // Public feed delivery is independent of personalized announcement eligibility.
    if (eventId && windowObject.signalR) {
      const shared = windowObject.bingoProgressConnection;
      const connection = shared || new windowObject.signalR.HubConnectionBuilder().withUrl("/hubs/progress").withAutomaticReconnect().build();
      const watch = () => connection.invoke("WatchEvent", eventId).then(reconcileLive).catch(() => {});
      connection.on("progressChanged", reconcileLive);
      connection.onreconnected(watch);
      if (shared) {
        if (connection.state === windowObject.signalR.HubConnectionState.Connected) watch();
        else windowObject.bingoProgressReady?.then(watch).catch(() => {});
      } else {
        windowObject.bingoProgressConnection = connection;
        windowObject.bingoProgressReady = connection.start();
        windowObject.bingoProgressReady.then(watch).catch(() => {});
      }
    }

    windowObject.addEventListener?.("drop-announcement-acknowledged", event => {
      const detail = event.detail || {};
      if (detail.eventId && detail.eventId !== eventId) return;
      const ids = Array.isArray(detail.submissionIds) && detail.submissionIds.length > 0
        ? detail.submissionIds
        : [...root.querySelectorAll("[data-public-recent-drop-id]")].map(card => card.dataset.publicRecentDropId);
      reconcileNewRows(ids);
    });

    root.addEventListener("click", event => {
      const link = event.target.closest?.("[data-public-recent-drops-nav]");
      if (!link || event.defaultPrevented || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

      const currentRegion = link.closest("[data-public-recent-drops]");
      if (!currentRegion) return;
      const target = new URL(link.href || link.getAttribute("href"), windowObject.location.href);
      if (target.origin !== windowObject.location.origin) return;

      event.preventDefault();
      replaceFeed(target, link);
    });
  }

  const api = { initialize };
  if (typeof window !== "undefined") {
    window.publicRecentDrops = api;
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", () => initialize());
    else initialize();
  }
  if (typeof module !== "undefined" && module.exports) module.exports = api;
})();

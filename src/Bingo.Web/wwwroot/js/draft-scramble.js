(() => {
  const adminText = key => document.body?.dataset[key] || "";
  let scramblePending = false;
  let scrambleAnimating = false;

  document.addEventListener("submit", event => {
    const form = event.target.closest?.("[data-draft-scramble-form]");
    if (!form) return;
    if (scrambleAnimating) {
      event.preventDefault();
      event.stopImmediatePropagation();
      return;
    }

    scramblePending = true;
    const status = form.querySelector("[data-draft-scramble-status]");
    const button = form.querySelector("[data-draft-scramble-submit]");
    if (status) status.textContent = adminText("adminDraftCreating");
    if (button) button.textContent = adminText("adminDraftDrawing");
  }, true);

  document.addEventListener("bingo:content-updated", event => {
    const selectors = event.detail?.selectors ?? [];
    if (!scramblePending || (!selectors.includes(".draft-teams-section") && !selectors.includes(".draft-live-shell"))) return;
    scramblePending = false;
    animateSavedOrder();
  });

  async function animateSavedOrder() {
    const grid = document.querySelector("[data-draft-team-grid]");
    const draftedTeams = grid ? [...grid.querySelectorAll("[data-draft-team='true']")] : [];
    if (!grid || draftedTeams.length < 2) return;

    const status = document.querySelector("[data-draft-scramble-status]");
    const button = document.querySelector("[data-draft-scramble-submit]");
    const finalOrder = [...draftedTeams];
    const otherTeams = [...grid.querySelectorAll(".team-card[data-draft-team='false']")];
    const finalRanks = new Map(finalOrder.map(card => [card, card.querySelector("[data-draft-rank]")?.textContent ?? ""]));

    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      if (status) status.textContent = adminText("adminDraftReady");
      return;
    }

    scrambleAnimating = true;
    if (button) button.disabled = true;
    grid.setAttribute("aria-busy", "true");
    grid.classList.add("is-scrambling");
    if (status) status.textContent = adminText("adminDraftDrawing");

    let currentOrder = [...finalOrder];
    for (const wait of [90, 110, 140, 180, 230, 300]) {
      currentOrder = shuffled(currentOrder);
      moveCards(grid, currentOrder, otherTeams);
      updateTemporaryRanks(currentOrder);
      await delay(wait);
    }

    moveCards(grid, finalOrder, otherTeams, true);
    finalRanks.forEach((rank, card) => {
      const label = card.querySelector("[data-draft-rank]");
      if (label) label.textContent = rank;
    });

    await delay(420);
    grid.classList.remove("is-scrambling");
    grid.removeAttribute("aria-busy");
    if (button) button.disabled = false;
    scrambleAnimating = false;
    if (status) status.textContent = adminText("adminDraftReady");
  }

  function moveCards(grid, draftedTeams, otherTeams, settling = false) {
    const cards = [...draftedTeams, ...otherTeams];
    const previous = new Map(cards.map(card => [card, card.getBoundingClientRect()]));

    cards.forEach(card => grid.append(card));

    cards.forEach(card => {
      const before = previous.get(card);
      const after = card.getBoundingClientRect();
      const x = before.left - after.left;
      const y = before.top - after.top;
      if (x === 0 && y === 0) return;

      card.animate(
        [
          { transform: `translate(${x}px, ${y}px)`, opacity: settling ? 0.82 : 0.68 },
          { transform: "translate(0, 0)", opacity: 1 }
        ],
        { duration: settling ? 360 : 170, easing: settling ? "cubic-bezier(.2,.8,.2,1)" : "ease-out" }
      );
    });
  }

  function updateTemporaryRanks(cards) {
    cards.forEach((card, index) => {
      const label = card.querySelector("[data-draft-rank]");
      if (label) label.textContent = `#${index + 1}`;
    });
  }

  function shuffled(cards) {
    const result = [...cards];
    for (let index = result.length - 1; index > 0; index--) {
      const target = Math.floor(Math.random() * (index + 1));
      [result[index], result[target]] = [result[target], result[index]];
    }
    return result;
  }

  const delay = milliseconds => new Promise(resolve => window.setTimeout(resolve, milliseconds));
})();

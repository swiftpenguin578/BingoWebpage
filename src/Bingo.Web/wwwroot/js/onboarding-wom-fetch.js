(function () {
  const copyFeedback = (target, source) => target.replaceChildren(...Array.from(source.childNodes, node => node.cloneNode(true)));

  const readResponse = (html, windowObject) => {
    const parsed = new windowObject.DOMParser().parseFromString(html, "text/html");
    const ehb = parsed.querySelector("[data-onboarding-ehb]");
    const feedback = parsed.querySelector("[data-onboarding-fetch-feedback]");
    const characterError = parsed.querySelector('[data-valmsg-for="Input.OsrsCharacterName"]');
    return { value: ehb?.value?.trim(), feedback, characterError };
  };

  const boundRoots = new WeakSet();
  const pendingForms = new WeakSet();

  function initialize(root = document, windowObject = window) {
    if (boundRoots.has(root)) return;
    boundRoots.add(root);
    root.addEventListener("click", async event => {
      const eventTarget = event.target;
      const button = eventTarget?.closest?.("[data-onboarding-fetch]") || eventTarget?.parentElement?.closest?.("[data-onboarding-fetch]");
      if (!button || (root.contains && !root.contains(button))) return;

      const form = button.closest?.("[data-onboarding-form]");
      const character = form?.querySelector("[data-onboarding-character]");
      const ehb = form?.querySelector("[data-onboarding-ehb]");
      const feedback = form?.querySelector("[data-onboarding-fetch-feedback]");
      const characterError = form?.querySelector('[data-valmsg-for="Input.OsrsCharacterName"]');
      if (!form || !feedback || !character || !ehb || pendingForms.has(form)) return;

      const showFailure = () => {
        feedback.textContent = form.dataset.onboardingFetchFailure || "";
        feedback.focus?.({ preventScroll: true });
      };
      event.preventDefault();
      const token = form.querySelector('input[name="__RequestVerificationToken"]');
      const fetchUrl = button.dataset?.onboardingFetchUrl;
      if (!token || !character.name || !fetchUrl) {
        showFailure();
        return;
      }

      pendingForms.add(form);
      form.setAttribute("aria-busy", "true");
      button.disabled = true;
      button.setAttribute("aria-disabled", "true");

      const payload = new windowObject.FormData();
      payload.append(token.name, token.value);
      payload.append(character.name, character.value);
      const target = new URL(fetchUrl, windowObject.location.href);
      let focusTarget = button;

      try {
        const response = await windowObject.fetch(target.href, {
          method: "POST",
          body: payload,
          credentials: "same-origin",
          headers: { Accept: "text/html", "X-Requested-With": "XMLHttpRequest" }
        });
        const responseUrl = response.url ? new URL(response.url, windowObject.location.href) : null;
        if (!response.ok || response.redirected || (responseUrl && responseUrl.pathname !== new URL(windowObject.location.href).pathname)) throw new Error("Onboarding lookup response was unusable.");

        const result = readResponse(await response.text(), windowObject);
        if (!result.value) {
          if (result.feedback?.textContent.trim()) copyFeedback(feedback, result.feedback);
          else if (result.characterError?.textContent.trim() && characterError) {
            copyFeedback(characterError, result.characterError);
            focusTarget = character;
          } else {
            showFailure();
            focusTarget = feedback;
          }
        } else {
          ehb.value = result.value;
          feedback.replaceChildren();
          characterError?.replaceChildren();
        }
      } catch {
        showFailure();
        focusTarget = feedback;
      } finally {
        pendingForms.delete(form);
        form.removeAttribute("aria-busy");
        button.disabled = false;
        button.removeAttribute("aria-disabled");
        focusTarget.focus?.({ preventScroll: true });
      }
    });
  }

  const api = { initialize };
  if (typeof window !== "undefined") {
    window.onboardingWomFetch = api;
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", () => initialize());
    else initialize();
  }
  if (typeof module !== "undefined" && module.exports) module.exports = api;
})();

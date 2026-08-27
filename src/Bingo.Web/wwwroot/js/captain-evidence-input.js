(() => {
  function showFile(zone, file) {
    if (!(zone instanceof HTMLElement) || !file) return;
    const input = zone.querySelector('input[type="file"]');
    const preview = zone.querySelector("[data-evidence-preview]");
    const empty = zone.querySelector("[data-evidence-empty]");
    const image = preview?.querySelector("img") ?? zone.querySelector("img");
    const previewButton = preview?.querySelector("[data-evidence-image]");
    const status = zone.querySelector("[data-evidence-status]");
    if (!(input instanceof HTMLInputElement) || !(image instanceof HTMLImageElement)) return;
    const transfer = new DataTransfer();
    transfer.items.add(file);
    input.files = transfer.files;
    if (zone.dataset.evidencePreviewUrl) URL.revokeObjectURL(zone.dataset.evidencePreviewUrl);
    const previewUrl = URL.createObjectURL(file);
    zone.dataset.evidencePreviewUrl = previewUrl;
    image.src = previewUrl;
    if (previewButton instanceof HTMLElement) previewButton.dataset.evidenceImage = previewUrl;
    if (empty instanceof HTMLElement && preview instanceof HTMLElement) {
      empty.hidden = true;
      preview.hidden = false;
      zone.classList.add("has-preview");
    } else {
      image.hidden = false;
    }
    if (status) {
      const template = zone.dataset.readyTemplate || "";
      status.textContent = template.replace("{0}", file.name || zone.dataset.pastedScreenshot || "");
    }
  }

  function removeFile(zone) {
    if (!(zone instanceof HTMLElement)) return;
    const input = zone.querySelector('input[type="file"]');
    const preview = zone.querySelector("[data-evidence-preview]");
    const empty = zone.querySelector("[data-evidence-empty]");
    const image = preview?.querySelector("img");
    const previewButton = preview?.querySelector("[data-evidence-image]");
    const status = zone.querySelector("[data-evidence-status]");
    if (zone.dataset.evidencePreviewUrl) URL.revokeObjectURL(zone.dataset.evidencePreviewUrl);
    delete zone.dataset.evidencePreviewUrl;
    if (input instanceof HTMLInputElement) {
      input.value = "";
      input.dispatchEvent(new Event("change", { bubbles: true }));
    }
    if (image instanceof HTMLImageElement) image.removeAttribute("src");
    if (previewButton instanceof HTMLElement) previewButton.removeAttribute("data-evidence-image");
    if (preview instanceof HTMLElement) preview.hidden = true;
    if (empty instanceof HTMLElement) empty.hidden = false;
    if (status instanceof HTMLElement) status.textContent = "";
    zone.classList.remove("has-preview");
  }

  document.addEventListener("dragover", event => {
    const zone = event.target.closest?.("[data-evidence-drop]");
    if (!zone) return;
    event.preventDefault();
    zone.classList.add("dragging");
  });
  document.addEventListener("dragleave", event => event.target.closest?.("[data-evidence-drop]")?.classList.remove("dragging"));
  document.addEventListener("drop", event => {
    const zone = event.target.closest?.("[data-evidence-drop]");
    if (!zone) return;
    event.preventDefault();
    zone.classList.remove("dragging");
    showFile(zone, Array.from(event.dataTransfer?.files ?? []).find(file => file.type.startsWith("image/")));
  });
  document.addEventListener("paste", event => {
    const zone = event.target.closest?.("[data-evidence-drop]");
    if (zone) showFile(zone, Array.from(event.clipboardData?.files ?? []).find(file => file.type.startsWith("image/")));
  });
  document.addEventListener("change", event => {
    const input = event.target;
    if (input instanceof HTMLInputElement && input.matches('[data-evidence-drop] input[type="file"]')) showFile(input.closest("[data-evidence-drop]"), input.files?.[0]);
  });
  document.addEventListener("click", async event => {
    const removeButton = event.target.closest?.("[data-evidence-remove]");
    if (removeButton) {
      event.preventDefault();
      removeFile(removeButton.closest("[data-evidence-drop]"));
      return;
    }
    const button = event.target.closest?.("[data-evidence-paste]");
    if (!button) return;
    const zone = button.closest("[data-evidence-drop]");
    const status = zone?.querySelector("[data-evidence-status]");
    try {
      const items = await navigator.clipboard.read();
      for (const item of items) {
        const type = item.types.find(value => value.startsWith("image/"));
        if (!type) continue;
        const blob = await item.getType(type);
        showFile(zone, new File([blob], "pasted-screenshot.png", { type }));
        return;
      }
      if (status) status.textContent = zone?.dataset.clipboardEmpty || "";
    } catch {
      if (status) status.textContent = zone?.dataset.clipboardBlocked || "";
      zone?.focus();
    }
  });
})();
